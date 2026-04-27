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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu AppId hop le.");
        }

        if (string.IsNullOrEmpty(key1))
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu Key1.");
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu Endpoint.");
        }

        var appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var randomDigits = Random.Shared.Next(100000, 1000000).ToString();
        var transId = $"{DateTime.UtcNow:yyMMdd}_{randomDigits}";
        donation.OrderId = transId;

        var amount = (long)(donation.Amount?.Amount ?? 0);
        var appUser = string.IsNullOrEmpty(donation.Donor?.Name) ? "Unknown" : donation.Donor.Name;
        var item = "[]";
        var description = $"RESQ - Ung ho chien dich {donation.FundCampaignCode}";

        var serverReturnUrl = ResolveReturnUrl(config);
        var embedData = JsonSerializer.Serialize(new { redirecturl = serverReturnUrl });

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
            bank_code = string.Empty,
            mac,
            callback_url = string.Empty
        };

        _logger.LogInformation(
            "ZaloPay create payment request | AppTransId={AppTransId} Amount={Amount} Endpoint={Endpoint} RedirectUrl={RedirectUrl}",
            transId,
            amount,
            endpoint,
            serverReturnUrl);

        var stopwatch = Stopwatch.StartNew();
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(endpoint, requestData, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "ZaloPay create payment response | AppTransId={AppTransId} DurationMs={DurationMs} StatusCode={StatusCode} Body={Body}",
            transId,
            stopwatch.ElapsedMilliseconds,
            (int)response.StatusCode,
            responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ZaloPay API HTTP Error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"Loi ket noi ZaloPay (HTTP {(int)response.StatusCode}).");
        }

        var result = JsonSerializer.Deserialize<ZaloPayCreateOrderResponse>(responseContent, JsonOptions);

        if (result == null || result.ReturnCode != 1)
        {
            _logger.LogError(
                "ZaloPay Business Error | AppTransId={AppTransId} ReturnCode={ReturnCode} ReturnMessage={ReturnMessage} SubReturnCode={SubReturnCode} SubReturnMessage={SubReturnMessage}",
                transId,
                result?.ReturnCode,
                result?.ReturnMessage,
                result?.SubReturnCode,
                result?.SubReturnMessage);
            throw new InvalidOperationException($"ZaloPay tu choi tao don hang: {result?.ReturnMessage} (Ma loi: {result?.ReturnCode})");
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
            var valid = computedMac.Equals(receivedMac, StringComparison.OrdinalIgnoreCase);
            if (!valid)
            {
                _logger.LogWarning("ZaloPay webhook mac mismatch.");
            }

            return valid;
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
            var appIdStr = config["AppId"];
            var key1 = config["Key1"];
            var queryEndpoint = config["QueryEndpoint"] ?? "https://sb-openapi.zalopay.vn/v2/query";

            if (!int.TryParse(appIdStr, out var appId) || string.IsNullOrEmpty(key1))
            {
                _logger.LogError("ZaloPay QueryOrder: missing AppId or Key1 configuration.");
                return null;
            }

            var macData = $"{appId}|{appTransId}|{key1}";
            var mac = ComputeHmacSha256(macData, key1);

            var requestData = new
            {
                app_id = appId,
                app_trans_id = appTransId,
                mac
            };

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(queryEndpoint, requestData, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "ZaloPay QueryOrder response | AppTransId={AppTransId} StatusCode={StatusCode} Body={Body}",
                appTransId,
                (int)response.StatusCode,
                responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ZaloPay QueryOrder HTTP error: {Status} - {Body}", response.StatusCode, responseContent);
                return null;
            }

            return JsonSerializer.Deserialize<ZaloPayQueryResponse>(responseContent, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZaloPay QueryOrder exception for AppTransId: {AppTransId}", appTransId);
            return null;
        }
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

    private string ResolveReturnUrl(IConfigurationSection config)
    {
        var configuredReturnUrl = config["ReturnUrl"];
        if (!string.IsNullOrWhiteSpace(configuredReturnUrl))
        {
            return configuredReturnUrl.TrimEnd('/');
        }

        var configuredCallbackUrl = config["CallbackUrl"];
        if (!string.IsNullOrWhiteSpace(configuredCallbackUrl)
            && configuredCallbackUrl.Contains("/zalopay-return", StringComparison.OrdinalIgnoreCase))
        {
            return configuredCallbackUrl.TrimEnd('/');
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl.TrimEnd('/')}/finance/donations/zalopay-return";
        }

        throw new InvalidOperationException("Cau hinh ZaloPay thieu ReturnUrl hoac AppSettings:BaseUrl.");
    }
}
