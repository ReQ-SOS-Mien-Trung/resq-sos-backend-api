using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RESQ.Infrastructure.Services.Payments;

public class MomoPaymentService : IPaymentGatewayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MomoPaymentService> _logger;

    public MomoPaymentService(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration,
        ILogger<MomoPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(DonationModel donation, CancellationToken cancellationToken = default)
    {
        // 1. Get Configuration
        var config = _configuration.GetSection("MomoAPI");
        
        // MoMo API v3 endpoint
        var endpoint = config["MomoApiUrl"] ?? "https://test-payment.momo.vn/v2/gateway/api/create";
        var partnerCode = config["PartnerCode"];
        var accessKey = config["AccessKey"];
        var secretKey = config["SecretKey"];
        
        var redirectUrl = config["RedirectUrl"] ?? config["ReturnUrl"];
        var ipnUrl = config["IpnUrl"];
        
        // requestType MUST be "captureWallet" for MoMo API v3 Wallet payment
        var requestType = "captureWallet";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(partnerCode) || string.IsNullOrEmpty(endpoint))
            throw new Exception("Momo configuration is missing (AccessKey, SecretKey, PartnerCode, MomoApiUrl).");

        if (string.IsNullOrEmpty(redirectUrl)) throw new Exception("Momo configuration requires RedirectUrl.");
        if (string.IsNullOrEmpty(ipnUrl)) throw new Exception("Momo configuration requires IpnUrl.");

        // 2. Prepare Data
        // OrderId must follow regex ^[0-9a-zA-Z]([-_.]*[0-9a-zA-Z]+)*$
        var orderId = donation.PayosOrderId;
        if (string.IsNullOrEmpty(orderId))
        {
            // Use PartnerCode as prefix to avoid test environment filtering
            orderId = $"{partnerCode}_{DateTime.UtcNow.Ticks}";
            donation.PayosOrderId = orderId;
        }
        
        var requestId = Guid.NewGuid().ToString("N");
        var amount = (long)(donation.Amount?.Amount ?? 0);

        // Validation: Amount 1,000 - 50,000,000
        if (amount < 1000 || amount > 50000000)
        {
            throw new Exception("Số tiền không hợp lệ. MoMo yêu cầu từ 1,000 đến 50,000,000 VND.");
        }

        var orderInfo = $"Ung ho {donation.FundCampaignCode ?? "RESQ"}";
        // Must be base64 encoded JSON string. e30= is base64 for "{}"
        var extraData = "e30=";
        
        // 3. Build Raw Signature String
        // Rule: Sort keys alphabetically
        var rawSignature =
            $"accessKey={accessKey}" +
            $"&amount={amount}" +
            $"&extraData={extraData}" +
            $"&ipnUrl={ipnUrl}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={partnerCode}" +
            $"&redirectUrl={redirectUrl}" +
            $"&requestId={requestId}" +
            $"&requestType={requestType}";

        _logger.LogInformation("Momo Raw Signature: {Signature}", rawSignature);

        // 4. Sign Data
        var signature = ComputeHmacSha256(rawSignature, secretKey);

        // 5. Create Request Object with exact properties matching MoMo documentation
        var requestData = new
        {
            partnerCode = partnerCode,
            partnerName = "Test",
            storeId = "MomoTestStore",
            requestId = requestId,
            amount = amount,
            orderId = orderId,
            orderInfo = orderInfo,
            redirectUrl = redirectUrl,
            ipnUrl = ipnUrl,
            requestType = requestType,
            extraData = extraData,
            lang = "vi",
            signature = signature
        };

        // 6. Send Request
        var client = _httpClientFactory.CreateClient();
        
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending MoMo Request to {Endpoint}: {Request}", endpoint, jsonContent);

        HttpResponseMessage response;
        try 
        {
            response = await client.PostAsync(endpoint, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MoMo API.");
            throw new Exception("Could not connect to payment gateway.");
        }

        // 7. Read & Log Response
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Log the full MoMo response before throwing an exception so the real error message can be seen
        _logger.LogInformation("MoMo Response: {Body}", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Momo API HTTP Error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            // Append the response body to the error message so the user can easily debug 400 Bad Request
            throw new Exception($"Momo API call failed with status code {response.StatusCode}. Response: {responseContent}");
        }

        // 8. Deserialize & Validate
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        MomoCreatePaymentResponse? momoResponse;
        try 
        {
            momoResponse = JsonSerializer.Deserialize<MomoCreatePaymentResponse>(responseContent, jsonOptions);
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "Failed to deserialize MoMo response.");
             throw new Exception("Invalid response format from MoMo gateway.");
        }

        if (momoResponse == null)
        {
            throw new Exception("Received null response from MoMo.");
        }

        if (momoResponse.ResultCode != 0)
        {
             _logger.LogError("Momo Business Error: Code {Code}, Message {Msg}", momoResponse.ResultCode, momoResponse.Message);
             throw new Exception($"Momo payment creation failed: {momoResponse.Message} (Code: {momoResponse.ResultCode})");
        }

        // 9. Return Result
        return new PaymentLinkResult
        {
            CheckoutUrl = momoResponse.PayUrl,
            PaymentLinkId = momoResponse.RequestId,
            OrderCode = orderId,
            QrCode = momoResponse.QrCodeUrl 
        };
    }

    public bool VerifyWebhookSignature(string jsonBody)
    {
        // Not used for generic validation in this architecture, specific logic is in ProcessMomoPaymentHandler
        return true; 
    }

    private string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(messageBytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
