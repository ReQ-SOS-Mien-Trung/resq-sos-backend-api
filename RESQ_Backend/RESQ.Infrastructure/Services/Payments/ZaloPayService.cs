using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RESQ.Infrastructure.Services.Payments;

public class ZaloPayService : IPaymentGatewayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ZaloPayService> _logger;

    public ZaloPayService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ZaloPayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(DonationModel donation, CancellationToken cancellationToken = default)
    {
        var config = _configuration.GetSection("ZaloPay");
        
        var appIdStr = config["AppId"];
        var key1 = config["Key1"];
        var endpoint = config["Endpoint"];

        if (string.IsNullOrEmpty(appIdStr) || !int.TryParse(appIdStr, out var appId))
            throw new InvalidOperationException("Cấu hình ZaloPay thiếu AppId hợp lệ.");

        if (string.IsNullOrEmpty(key1))
            throw new InvalidOperationException("Cấu hình ZaloPay thiếu Key1.");
            
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("Cấu hình ZaloPay thiếu Endpoint.");

        var appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // ZaloPay trans ID format requires yyMMdd_xxxxxx format (6 random digits)
        var randomDigits = Random.Shared.Next(100000, 1000000).ToString();
        var transId = $"{DateTime.UtcNow:yyMMdd}_{randomDigits}";
        donation.OrderId = transId;

        var amount = (long)(donation.Amount?.Amount ?? 0);
        var appUser = string.IsNullOrEmpty(donation.Donor?.Name) ? "Unknown" : donation.Donor.Name;
        var item = "[]"; 
        var description = $"RESQ - Ủng hộ chiến dịch {donation.FundCampaignCode}";
        
        // Redirect through backend so it can verify the payment before sending the user to the frontend.
        // ZaloPay will append ?appid=&apptransid=&pmcid=&bankcode=&amount=&discountamount=&status= to this URL.
        // Use ZaloPay:CallbackUrl directly (set via env var ZaloPay__CallbackUrl) to avoid AppSettings:BaseUrl dependency.
        var serverReturnUrl = config["CallbackUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(serverReturnUrl))
            throw new InvalidOperationException("Cấu hình ZaloPay thiếu CallbackUrl (zalopay-return endpoint).");
        var embedDataObj = new { redirecturl = serverReturnUrl };
        var embedData = JsonSerializer.Serialize(embedDataObj);

        // MAC formula for Order creation: app_id|app_trans_id|app_user|amount|app_time|embed_data|item
        var macData = $"{appId}|{transId}|{appUser}|{amount}|{appTime}|{embedData}|{item}";
        var mac = ComputeHmacSha256(macData, key1);

        var requestData = new 
        {
            app_id = appId,
            app_user = appUser,
            app_trans_id = transId,
            app_time = appTime,
            amount = amount,
            item = item,
            description = description,
            embed_data = embedData,
            bank_code = "",
            mac = mac,
            callback_url = ""  // IPN not needed - zalopay-return redirect handles verification
        };

        var client = _httpClientFactory.CreateClient();
        
        var response = await client.PostAsJsonAsync(endpoint, requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ZaloPay API HTTP Error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"Lỗi kết nối ZaloPay (HTTP {(int)response.StatusCode}).");
        }

        var result = JsonSerializer.Deserialize<ZaloPayCreateOrderResponse>(responseContent, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || result.ReturnCode != 1)
        {
            _logger.LogError("ZaloPay Business Error: {Code} - {Msg}", result?.ReturnCode, result?.ReturnMessage);
            throw new InvalidOperationException($"ZaloPay từ chối tạo đơn hàng: {result?.ReturnMessage} (Mã lỗi: {result?.ReturnCode})");
        }

        return new PaymentLinkResult
        {
            CheckoutUrl = result.OrderUrl, 
            PaymentLinkId = result.ZpTransToken,
            OrderCode = transId,
            QrCode = result.OrderUrl 
        };
    }

    public bool VerifyWebhookSignature(string jsonBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataElement) ||
                !root.TryGetProperty("mac", out var macElement))
            {
                _logger.LogWarning("ZaloPay webhook missing data or mac property.");
                return false;
            }

            // Extract the stringified payload and MAC properties
            var dataStr = dataElement.GetString();
            var receivedMac = macElement.GetString();

            if (string.IsNullOrEmpty(dataStr) || string.IsNullOrEmpty(receivedMac))
            {
                _logger.LogWarning("ZaloPay webhook data or mac is empty.");
                return false;
            }

            var key2 = _configuration["ZaloPay:Key2"];
            if (string.IsNullOrEmpty(key2))
            {
                _logger.LogError("ZaloPay configuration is missing Key2 for webhook verification.");
                return false;
            }

            var computedMac = ComputeHmacSha256(dataStr, key2);

            return computedMac.Equals(receivedMac, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying ZaloPay webhook signature.");
            return false;
        }
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

    public async Task<ZaloPayQueryResponse?> QueryOrderAsync(string appTransId, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _configuration.GetSection("ZaloPay");
            var appIdStr = config["AppId"];
            var key1 = config["Key1"];
            // Sandbox query endpoint; production: https://openapi.zalopay.vn/v2/query
            var queryEndpoint = config["QueryEndpoint"] ?? "https://sb-openapi.zalopay.vn/v2/query";

            if (!int.TryParse(appIdStr, out var appId) || string.IsNullOrEmpty(key1))
            {
                _logger.LogError("ZaloPay QueryOrder: missing AppId or Key1 configuration.");
                return null;
            }

            // MAC formula: HMAC-SHA256(key1, app_id|app_trans_id|key1)
            var macData = $"{appId}|{appTransId}|{key1}";
            var mac = ComputeHmacSha256(macData, key1);

            // app_id MUST be an integer in JSON - using Dictionary<string,string> would
            // serialize it as "554" (string) which ZaloPay rejects.
            var requestData = new
            {
                app_id = appId,        // int → serialized as 554
                app_trans_id = appTransId,
                mac = mac
            };

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(queryEndpoint, requestData, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("ZaloPay QueryOrder response for {AppTransId}: {Response}", appTransId, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ZaloPay QueryOrder HTTP error: {Status} - {Body}", response.StatusCode, responseContent);
                return null;
            }

            return JsonSerializer.Deserialize<ZaloPayQueryResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZaloPay QueryOrder exception for AppTransId: {AppTransId}", appTransId);
            return null;
        }
    }
}
