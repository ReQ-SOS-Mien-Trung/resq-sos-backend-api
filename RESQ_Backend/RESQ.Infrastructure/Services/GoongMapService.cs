using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services;

public class GoongMapService : IGoongMapService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<GoongMapService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GoongMapService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoongMapService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Goong");
        _apiKey = configuration["Goong:ApiKey"]
            ?? throw new InvalidOperationException("Goong API key chưa được cấu hình.");
        _baseUrl = configuration["Goong:DirectionBaseUrl"] ?? "https://rsapi.goong.io";
        _logger = logger;
    }

    public async Task<GoongRouteResult> GetRouteAsync(
        double originLat,
        double originLng,
        double destLat,
        double destLng,
        string vehicle = "car",
        CancellationToken cancellationToken = default)
    {
        // Build URL - coordinates formatted as "lat,lng"
        var origin = $"{originLat.ToString(CultureInfo.InvariantCulture)},{originLng.ToString(CultureInfo.InvariantCulture)}";
        var destination = $"{destLat.ToString(CultureInfo.InvariantCulture)},{destLng.ToString(CultureInfo.InvariantCulture)}";
        var url = $"{_baseUrl}/Direction?origin={origin}&destination={destination}&vehicle={vehicle}&api_key={_apiKey}";

        _logger.LogInformation(
            "Calling Goong Direction API: origin={Origin} dest={Destination} vehicle={Vehicle}",
            origin, destination, vehicle);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Goong Direction API call failed");
            return new GoongRouteResult { Status = "ERROR", ErrorMessage = ex.Message };
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Goong API returned HTTP {StatusCode}: {Body}", response.StatusCode, body);
            return new GoongRouteResult
            {
                Status = "ERROR",
                ErrorMessage = $"HTTP {(int)response.StatusCode}: {body}"
            };
        }

        GoongDirectionResponse? goongResp;
        try
        {
            goongResp = await response.Content.ReadFromJsonAsync<GoongDirectionResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Goong Direction response");
            return new GoongRouteResult { Status = "ERROR", ErrorMessage = "Không thể đọc kết quả từ Goong API." };
        }

        if (goongResp is null || goongResp.Status != "OK" || goongResp.Routes is null || goongResp.Routes.Count == 0)
        {
            return new GoongRouteResult
            {
                Status = goongResp?.Status ?? "UNKNOWN",
                ErrorMessage = "Goong API không trả về tuyến đường."
            };
        }

        // Pick the first (best) route
        var bestRoute = goongResp.Routes[0];
        var leg = bestRoute.Legs?.FirstOrDefault();

        var steps = leg?.Steps?.Select(s => new GoongRouteStep
        {
            Instruction = StripHtml(s.HtmlInstructions ?? string.Empty),
            DistanceMeters = s.Distance?.Value ?? 0,
            DistanceText = s.Distance?.Text ?? string.Empty,
            DurationSeconds = s.Duration?.Value ?? 0,
            DurationText = s.Duration?.Text ?? string.Empty,
            Maneuver = s.Maneuver ?? string.Empty,
            StartLat = s.StartLocation?.Lat ?? 0,
            StartLng = s.StartLocation?.Lng ?? 0,
            EndLat = s.EndLocation?.Lat ?? 0,
            EndLng = s.EndLocation?.Lng ?? 0,
            Polyline = s.Polyline?.Points ?? string.Empty
        }).ToList() ?? [];

        return new GoongRouteResult
        {
            Status = "OK",
            Route = new GoongRouteSummary
            {
                TotalDistanceMeters = leg?.Distance?.Value ?? 0,
                TotalDistanceText = leg?.Distance?.Text ?? string.Empty,
                TotalDurationSeconds = leg?.Duration?.Value ?? 0,
                TotalDurationText = leg?.Duration?.Text ?? string.Empty,
                OverviewPolyline = bestRoute.OverviewPolyline?.Points ?? string.Empty,
                Summary = bestRoute.Summary ?? string.Empty,
                Steps = steps
            }
        };
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
    }

    // ---- Internal DTOs for Goong API response deserialization ----

    private sealed class GoongDirectionResponse
    {
        public string? Status { get; set; }
        public List<GoongRoute>? Routes { get; set; }
    }

    private sealed class GoongRoute
    {
        public string? Summary { get; set; }
        public List<GoongLeg>? Legs { get; set; }
        public GoongPolyline? OverviewPolyline { get; set; }
    }

    private sealed class GoongLeg
    {
        public GoongTextValue? Distance { get; set; }
        public GoongTextValue? Duration { get; set; }
        public string? StartAddress { get; set; }
        public string? EndAddress { get; set; }
        public List<GoongStep>? Steps { get; set; }
    }

    private sealed class GoongStep
    {
        public GoongTextValue? Distance { get; set; }
        public GoongTextValue? Duration { get; set; }
        public GoongLatLng? StartLocation { get; set; }
        public GoongLatLng? EndLocation { get; set; }
        public string? HtmlInstructions { get; set; }
        public string? Maneuver { get; set; }
        public GoongPolyline? Polyline { get; set; }
    }

    private sealed class GoongTextValue
    {
        public string? Text { get; set; }
        public int Value { get; set; }
    }

    private sealed class GoongLatLng
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    private sealed class GoongPolyline
    {
        public string? Points { get; set; }
    }
}
