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

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(checksumKey) || string.IsNullOrEmpty(returnUrl) || string.IsNullOrEmpty(cancelUrl))
        {
            throw new InvalidOperationException("PayOS configuration is missing.");
        }

        // 1. Prepare Data
        long orderCode;
        if (!string.IsNullOrEmpty(donation.PayosOrderId) && long.TryParse(donation.PayosOrderId, out var existingCode))
        {
            orderCode = existingCode;
        }
        else
        {
            // Fallback
            orderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss"));
            donation.PayosOrderId = orderCode.ToString();
        }

        // Requirement 1: Description format "Donation #{donation.Id} - {campaign.Code}"
        // Fallback to CampaignId if Code is null
        var campaignRef = !string.IsNullOrEmpty(donation.FundCampaignCode) ? donation.FundCampaignCode : donation.FundCampaignId.ToString();
        //var description = $"Donation #{donation.Id} - {campaignRef}";
        var description = $"RESQ{donation.Id}";

        // Truncate description if too long (PayOS limit usually 25 chars for some fields, but description supports more. Verify API docs. Usually 255 chars).
        // Standardizing safe length.
        if (description.Length > 25) 
        {
             // PayOS requires description to be short in some contexts, but usually standard payment link allows longer.
             // If strict 25 char limit applies to 'description' field in signature, we must be careful.
             // PayOS Docs: description should be short. Let's assume standard behavior.
             // Actually, PayOS 'description' typically maps to banking transaction content, which is very limited.
             // "Donation #1023 - FLOOD" might fit. "Donation #1023 - FLOOD-MT2026" is 29 chars.
             // Let's try to fit or truncate.
             if (description.Length > 25) description = description.Substring(0, 25);
        }

        // Extract amount
        var amount = (int)(donation.Amount?.Amount ?? 0);

        // Extract Buyer Info
        var buyerName = donation.Donor?.Name;
        var buyerEmail = donation.Donor?.Email; // Requirement 2 support

        // 2. Create Signature
        // Signature requires alphabetical order of fields.
        // amount, cancelUrl, description, orderCode, returnUrl
        var signatureData =
            $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        var signature = CreateSignature(signatureData, checksumKey);

        var requestData = new PayOSCreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = amount,
            Description = description,
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl,
            Signature = signature,

            BuyerName = buyerName,
            BuyerEmail = buyerEmail, 
            Items = [
                new PayOSItem { Name = $"Ủng hộ chiến dịch {campaignRef}", Quantity = 1, Price = amount }
            ]
        };

        // 3. Send Request
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl ?? "https://api-merchant.payos.vn");
        client.DefaultRequestHeaders.Add("x-client-id", clientId);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);

        _logger.LogInformation("Sending Payment Request to PayOS: OrderCode={OrderCode}, Amount={Amount}, Desc={Desc}", orderCode, amount, description);

        var response = await client.PostAsJsonAsync("/v2/payment-requests", requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayOS API Error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            throw new Exception($"Lỗi khi tạo link thanh toán PayOS: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<PayOSResponse<PayOSPaymentLinkData>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || result.Code != "00" || result.Data == null)
        {
            _logger.LogError("PayOS Logic Error: {Code} - {Desc}", result?.Code, result?.Desc);
            throw new Exception($"Lỗi xử lý PayOS: {result?.Desc}");
        }

        return new PaymentLinkResult
        {
            CheckoutUrl = result.Data.CheckoutUrl,
            PaymentLinkId = result.Data.PaymentLinkId,
            OrderCode = orderCode.ToString(),
            QrCode = result.Data.QrCode
        };
    }

    private static string CreateSignature(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
