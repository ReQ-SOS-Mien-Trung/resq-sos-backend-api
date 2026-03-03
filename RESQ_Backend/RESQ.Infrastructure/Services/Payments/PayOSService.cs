using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RESQ.Infrastructure.Dtos.Finance;
using System.Collections.Generic;
using System.Linq;

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

        // Logic to create link (kept from original)
        long orderCode;
        if (!string.IsNullOrEmpty(donation.PayosOrderId) && long.TryParse(donation.PayosOrderId, out var existingCode))
            orderCode = existingCode;
        else
        {
            orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss"));
            donation.PayosOrderId = orderCode.ToString();
        }

        var campaignCode = donation.FundCampaignCode ?? "CAMP";
        var description = $"Donation #{donation.Id} - {campaignCode}";
        if (description.Length > 25) description = description.Substring(0, 25);

        var amount = (int)(donation.Amount?.Amount ?? 0);
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();

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
            Items = [ new PayOSItem { Name = $"Ủng hộ {campaignCode}", Quantity = 1, Price = amount } ]
        };

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl ?? "https://api-merchant.payos.vn");
        client.DefaultRequestHeaders.Add("x-client-id", clientId);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var response = await client.PostAsJsonAsync("/v2/payment-requests", requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Lỗi PayOS ({response.StatusCode}): {responseContent}");

        var result = JsonSerializer.Deserialize<PayOSResponse<PayOSPaymentLinkData>>(responseContent, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || result.Code != "00" || result.Data == null)
            throw new Exception($"Lỗi PayOS Logic: {result?.Desc}");

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
                _logger.LogWarning("Webhook missing 'data' or 'signature' properties.");
                return false;
            }

            var signatureReceived = signatureElement.GetString();
            if (string.IsNullOrEmpty(signatureReceived)) return false;

            var checksumKey = _configuration["PayOS:ChecksumKey"];
            if (string.IsNullOrEmpty(checksumKey))
            {
                _logger.LogError("ChecksumKey configuration is missing.");
                return false;
            }

            // Flatten 'data' object to SortedDictionary
            var dataDict = new SortedDictionary<string, object>();
            foreach (var prop in dataElement.EnumerateObject())
            {
                var value = prop.Value;
                // PayOS Rules: 
                // 1. Ignore nulls
                // 2. Ignore nested objects (though data usually flat)
                // 3. Convert numbers to string, strings keep as is
                
                if (value.ValueKind == JsonValueKind.Null) continue;

                if (value.ValueKind == JsonValueKind.String)
                {
                     dataDict.Add(prop.Name, value.GetString() ?? "");
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    dataDict.Add(prop.Name, value.ToString());
                }
                else if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                     dataDict.Add(prop.Name, value.ToString().ToLower());
                }
            }

            // Build signature string
            var dataStringBuilder = new StringBuilder();
            foreach (var kvp in dataDict)
            {
                if (dataStringBuilder.Length > 0)
                    dataStringBuilder.Append('&');

                dataStringBuilder.Append($"{kvp.Key}={kvp.Value}");
            }

            var computedSignature = CreateSignature(dataStringBuilder.ToString(), checksumKey);

            // Logging for Debugging
            _logger.LogInformation("--- PayOS Signature Verification ---");
            _logger.LogInformation("Raw Body Length: {Length}", jsonBody.Length);
            _logger.LogInformation("Computed Data String: {DataString}", dataStringBuilder.ToString());
            _logger.LogInformation("Computed Signature: {Computed}", computedSignature);
            _logger.LogInformation("Received Signature: {Received}", signatureReceived);
            
            if (computedSignature != signatureReceived)
            {
                _logger.LogWarning("MISMATCH! Check key or field filtering.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature.");
            return false;
        }
    }

    private static string CreateSignature(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
