using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Infrastructure.Dtos.Finance;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RESQ.Infrastructure.Services.Payments;

public class PayOSService : IPaymentGatewayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PayOSService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(DonationModel donation, CancellationToken cancellationToken = default)
    {
        var payOsConfig = _configuration.GetSection("PayOS");
        var clientId = payOsConfig["ClientId"];
        var apiKey = payOsConfig["ApiKey"];
        var checksumKey = payOsConfig["ChecksumKey"];
        var baseUrl = payOsConfig["BaseUrl"]?.TrimEnd('/');
        var returnUrl = payOsConfig["ReturnUrl"];
        var cancelUrl = payOsConfig["CancelUrl"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(checksumKey))
        {
            throw new InvalidOperationException("Cấu hình PayOS bị thiếu hoặc không hợp lệ.");
        }

        long orderCode;
        if (!string.IsNullOrEmpty(donation.PayosOrderId) && long.TryParse(donation.PayosOrderId, out var existingCode))
            orderCode = existingCode;
        else
        {
            orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss"));
            donation.PayosOrderId = orderCode.ToString();
        }

        var campaignCode = donation.FundCampaignCode ?? "CAMP";
        var description = $"Donation {donation.Id} {campaignCode}";
        description = System.Text.RegularExpressions.Regex.Replace(description, "[^a-zA-Z0-9 ]", "");
        if (description.Length > 25) description = description.Substring(0, 25);

        var amount = (int)(donation.Amount?.Amount ?? 0);
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

        // Signature for Creating Link (order matters: amount, cancelUrl, description, orderCode, returnUrl)
        // Ensure values are sorted by key for signature generation if building manually, 
        // or ensure the properties used here match PayOS requirements.
        var signatureData = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        var signature = CreateSignature(signatureData, checksumKey);

        var requestData = new PayOSCreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = amount,
            Description = description,
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl,
            Signature = signature,
            ExpiredAt = expiredAt,
            BuyerName = donation.Donor?.Name,
            BuyerEmail = donation.Donor?.Email,
            Items = [ new PayOSItem { Name = $"Ung ho {campaignCode}", Quantity = 1, Price = amount } ]
        };

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl ?? "https://api-merchant.payos.vn");
        client.DefaultRequestHeaders.Add("x-client-id", clientId);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var response = await client.PostAsJsonAsync("/v2/payment-requests", requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayOS Error: {Content}", responseContent);
            throw new Exception($"Lỗi tạo link thanh toán ({response.StatusCode})");
        }

        var result = JsonSerializer.Deserialize<PayOSResponse<PayOSPaymentLinkData>>(responseContent, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || result.Code != "00" || result.Data == null)
            throw new Exception($"Lỗi PayOS: {result?.Desc}");

        return new PaymentLinkResult
        {
            CheckoutUrl = result.Data.CheckoutUrl,
            PaymentLinkId = result.Data.PaymentLinkId,
            OrderCode = orderCode.ToString(),
            QrCode = result.Data.QrCode
        };
    }

    public bool VerifyWebhookSignature(string jsonBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataElement) ||
                !root.TryGetProperty("signature", out var signatureElement))
            {
                _logger.LogWarning("Webhook thiếu data hoặc signature.");
                return false;
            }

            var receivedSignature = signatureElement.GetString();
            if (string.IsNullOrEmpty(receivedSignature))
            {
                _logger.LogWarning("Signature rỗng.");
                return false;
            }

            var checksumKey = _configuration["PayOS:ChecksumKey"];
            if (string.IsNullOrEmpty(checksumKey))
            {
                _logger.LogError("Thiếu cấu hình PayOS ChecksumKey.");
                return false;
            }

            var dataString = BuildSignatureDataString(dataElement);

            var computedSignature = CreateSignature(dataString, checksumKey);

            _logger.LogInformation("PayOS Verify DataString: {DataString}", dataString);
            _logger.LogInformation("Computed Signature: {Computed}", computedSignature);
            _logger.LogInformation("Received Signature: {Received}", receivedSignature);

            return computedSignature.Equals(receivedSignature, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi verify PayOS webhook.");
            return false;
        }
    }

    private static string BuildSignatureDataString(JsonElement dataElement)
    {
        var dict = new Dictionary<string, string>();

        foreach (var prop in dataElement.EnumerateObject())
        {
            // PayOS Java: value = jsonObject.get(key).toString()
            var value = prop.Value.ToString();

            dict[prop.Name] = value ?? "";
        }

        var sorted = dict.OrderBy(x => x.Key, StringComparer.Ordinal);

        var builder = new StringBuilder();

        foreach (var item in sorted)
        {
            if (builder.Length > 0)
                builder.Append("&");

            builder.Append(item.Key);
            builder.Append("=");
            builder.Append(item.Value);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Flattens, filters nulls (keeps empty strings), and sorts keys by ASCII rules.
    /// </summary>
    private Dictionary<string, string> SortAndFilterData(JsonNode dataNode)
    {
        var result = new Dictionary<string, string>();

        if (dataNode is JsonObject jsonObj)
        {
            foreach (var kvp in jsonObj)
            {
                var key = kvp.Key;
                var valueNode = kvp.Value;

                // Ignore null values strictly
                if (valueNode == null) continue;

                string stringValue;

                // Handle types to preserve formatting (especially numbers)
                if (valueNode is JsonValue val)
                {
                    if (val.TryGetValue<string>(out var s))
                    {
                        stringValue = s; // Keep strings as is (including empty)
                    }
                    else
                    {
                        // For numbers/bools, use Raw Text to ensure 500000 matches 500000 (not 500000.0)
                        stringValue = valueNode.ToJsonString();
                    }
                }
                else 
                {
                    // Fallback for arrays/objects if they exist in simple flat payload (usually shouldn't)
                    stringValue = valueNode.ToJsonString();
                }

                result[key] = stringValue;
            }
        }

        // Sort keys by ASCII (Ordinal)
        return result
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private static string CreateSignature(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
