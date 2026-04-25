using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Diagnostics;
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
        var appId = GetRequiredAppId(config);
        var key1 = GetRequiredValue(config, "Key1", "Cau hinh ZaloPay thieu Key1.");
        var endpoint = GetRequiredValue(config, "Endpoint", "Cau hinh ZaloPay thieu Endpoint.");
        var callbackUrl = GetRequiredValue(
            config,
            "CallbackUrl",
            "Cau hinh ZaloPay thieu CallbackUrl (endpoint redirect / zalopay-return).").TrimEnd('/');

        var requestStopwatch = Stopwatch.StartNew();

        // ZaloPay timeout is implicit from app_time, so keep the create payload minimal.
        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var appTime = vietnamNow.ToUnixTimeMilliseconds();
        var randomDigits = Random.Shared.Next(100000, 1000000).ToString();
        var transId = $"{vietnamNow:yyMMdd}_{randomDigits}";
        donation.OrderId = transId;

        var amount = (long)(donation.Amount?.Amount ?? 0);
        var appUser = string.IsNullOrWhiteSpace(donation.Donor?.Name) ? "Unknown" : donation.Donor.Name;
        var item = "[]";
        var description = $"RESQ - Ung ho chien dich {donation.FundCampaignCode}";
        var embedData = JsonSerializer.Serialize(new { redirecturl = callbackUrl });

        // MAC formula: app_id|app_trans_id|app_user|amount|app_time|embed_data|item
        var macData = $"{appId}|{transId}|{appUser}|{amount}|{appTime}|{embedData}|{item}";
        var mac = ComputeHmacSha256(macData, key1);

        var requestData = new
        {
            app_id = appId,
            app_user = appUser,
            app_trans_id = transId,
            app_time = appTime,
            amount,
            item,
            description,
            embed_data = embedData,
            callback_url = callbackUrl,
            mac
        };

        _logger.LogInformation(
            "Creating ZaloPay payment request for DonationId={DonationId}, AppTransId={AppTransId}, Endpoint={Endpoint}, Payload={Payload}",
            donation.Id,
            transId,
            endpoint,
            JsonSerializer.Serialize(new
            {
                requestData.app_id,
                requestData.app_trans_id,
                requestData.app_user,
                requestData.amount,
                requestData.app_time,
                requestData.description,
                requestData.item,
                requestData.embed_data,
                requestData.callback_url,
                mac = MaskMac(requestData.mac)
            }));

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(endpoint, requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        requestStopwatch.Stop();

        _logger.LogInformation(
            "ZaloPay create order response for AppTransId={AppTransId} received in {ElapsedMs}ms with HTTP {StatusCode}: {Response}",
            transId,
            requestStopwatch.ElapsedMilliseconds,
            (int)response.StatusCode,
            responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ZaloPay API HTTP error for AppTransId={AppTransId}: {StatusCode} - {Content}", transId, response.StatusCode, responseContent);
            throw new HttpRequestException($"Loi ket noi ZaloPay (HTTP {(int)response.StatusCode}).");
        }

        var result = JsonSerializer.Deserialize<ZaloPayCreateOrderResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || result.ReturnCode != 1)
        {
            _logger.LogError(
                "ZaloPay create order business error for AppTransId={AppTransId}: return_code={ReturnCode}, sub_return_code={SubReturnCode}, message={Message}",
                transId,
                result?.ReturnCode,
                result?.SubReturnCode,
                result?.ReturnMessage);
            throw new InvalidOperationException($"ZaloPay tu choi tao don hang: {result?.ReturnMessage} (Ma loi: {result?.ReturnCode})");
        }

        _logger.LogInformation(
            "ZaloPay payment link created successfully for DonationId={DonationId}, AppTransId={AppTransId}, ZpTransToken={ZpTransToken}",
            donation.Id,
            transId,
            result.ZpTransToken);

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

    public async Task<ZaloPayQueryResponse?> QueryOrderAsync(string appTransId, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _configuration.GetSection("ZaloPay");
            var appId = GetRequiredAppId(config);
            var key1 = GetRequiredValue(config, "Key1", "Cau hinh ZaloPay thieu Key1.");
            var queryEndpoint = config["QueryEndpoint"] ?? "https://sb-openapi.zalopay.vn/v2/query";
            var stopwatch = Stopwatch.StartNew();

            var macData = $"{appId}|{appTransId}|{key1}";
            var mac = ComputeHmacSha256(macData, key1);

            var requestData = new
            {
                app_id = appId,
                app_trans_id = appTransId,
                mac
            };

            _logger.LogInformation(
                "Querying ZaloPay order status for AppTransId={AppTransId} at Endpoint={Endpoint}",
                appTransId,
                queryEndpoint);

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(queryEndpoint, requestData, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "ZaloPay QueryOrder response for AppTransId={AppTransId} received in {ElapsedMs}ms with HTTP {StatusCode}: {Response}",
                appTransId,
                stopwatch.ElapsedMilliseconds,
                (int)response.StatusCode,
                responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ZaloPay QueryOrder HTTP error for AppTransId={AppTransId}: {Status} - {Body}", appTransId, response.StatusCode, responseContent);
                return null;
            }

            return JsonSerializer.Deserialize<ZaloPayQueryResponse>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZaloPay QueryOrder exception for AppTransId={AppTransId}", appTransId);
            return null;
        }
    }

    private static int GetRequiredAppId(IConfigurationSection config)
    {
        var appIdStr = config["AppId"];
        if (string.IsNullOrWhiteSpace(appIdStr) || !int.TryParse(appIdStr, out var appId))
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu AppId hop le.");
        }

        return appId;
    }

    private static string GetRequiredValue(IConfigurationSection config, string key, string errorMessage)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private static string MaskMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac) || mac.Length <= 8)
        {
            return "***";
        }

        return $"{mac[..4]}***{mac[^4..]}";
    }
}
