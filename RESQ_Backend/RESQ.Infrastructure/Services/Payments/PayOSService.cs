using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Models.Finance.PayOS;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;

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
        if (!string.IsNullOrEmpty(donation.OrderId) && long.TryParse(donation.OrderId, out var existingCode))
            orderCode = existingCode;
        else
        {
            orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss"));
            donation.OrderId = orderCode.ToString();
        }

        var campaignCode = donation.FundCampaignCode ?? "CAMP";
        var description = $"Donation {donation.Id} {campaignCode}";
        description = System.Text.RegularExpressions.Regex.Replace(description, "[^a-zA-Z0-9 ]", "");
        if (description.Length > 25) description = description.Substring(0, 25);

        var amount = (int)(donation.Amount?.Amount ?? 0);
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

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
            throw new Exception($"Lỗi tạo link thanh toán (HTTP {(int)response.StatusCode}).");
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
            var value = prop.Value.ToString();
            dict[prop.Name] = value ?? "";
        }
        var sorted = dict.OrderBy(x => x.Key, StringComparer.Ordinal);
        var builder = new StringBuilder();
        foreach (var item in sorted)
        {
            if (builder.Length > 0) builder.Append("&");
            builder.Append(item.Key);
            builder.Append("=");
            builder.Append(item.Value);
        }
        return builder.ToString();
    }

    private static string CreateSignature(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    public Task<ZaloPayQueryResponse?> QueryOrderAsync(string appTransId, CancellationToken cancellationToken = default)
        => Task.FromResult<ZaloPayQueryResponse?>(null); // Not supported by PayOS

    /// <summary>
    /// Queries PayOS for a payment link's current status via
    /// GET /v2/payment-requests/{paymentLinkId}.
    /// Returns a normalised <see cref="ZaloPayQueryResponse"/> so callers can
    /// share the same logic as ZaloPay verify:
    ///   ReturnCode 1 = PAID, 2 = failed/cancelled/expired, 3 = processing
    /// </summary>
    public async Task<ZaloPayQueryResponse?> QueryPaymentLinkAsync(string paymentLinkId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payOsConfig = _configuration.GetSection("PayOS");
            var clientId = payOsConfig["ClientId"];
            var apiKey = payOsConfig["ApiKey"];
            var baseUrl = (payOsConfig["BaseUrl"]?.TrimEnd('/')) ?? "https://api-merchant.payos.vn";

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("PayOS QueryPaymentLink: missing ClientId or ApiKey configuration.");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("x-client-id", clientId);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var response = await client.GetAsync($"/v2/payment-requests/{paymentLinkId}", cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("PayOS QueryPaymentLink response for {PaymentLinkId}: {Response}", paymentLinkId, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayOS QueryPaymentLink HTTP error: {Status} - {Body}", response.StatusCode, responseContent);
                return null;
            }

            var result = JsonSerializer.Deserialize<PayOSOrderStatusResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Data == null || result.Code != "00")
            {
                _logger.LogWarning("PayOS QueryPaymentLink business error: {Code} - {Desc}", result?.Code, result?.Desc);
                return null;
            }

            var status = result.Data.Status?.ToUpperInvariant();
            int returnCode = status switch
            {
                "PAID"       => 1,
                "PROCESSING" => 3,
                _            => 2   // CANCELLED, EXPIRED, or unknown
            };

            return new ZaloPayQueryResponse
            {
                ReturnCode    = returnCode,
                ReturnMessage = result.Data.Status ?? string.Empty,
                Amount        = result.Data.Amount,
                ZpTransId     = 0,   // not applicable for PayOS; caller must not overwrite TransactionId
                ServerTime    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayOS QueryPaymentLink exception for PaymentLinkId: {PaymentLinkId}", paymentLinkId);
            return null;
        }
    }
}

