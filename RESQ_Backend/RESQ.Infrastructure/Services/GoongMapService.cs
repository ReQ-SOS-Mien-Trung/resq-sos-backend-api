using System.Globalization;
using System.Text;
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
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
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
        string rawBody;
        try
        {
            rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Goong raw response: {Body}", rawBody);
            goongResp = JsonSerializer.Deserialize<GoongDirectionResponse>(rawBody, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Goong Direction response");
            return new GoongRouteResult { Status = "ERROR", ErrorMessage = "Không thể đọc kết quả từ Goong API." };
        }

        if (goongResp?.Routes is null || goongResp.Routes.Count == 0)
        {
            return new GoongRouteResult
            {
                Status = "NO_ROUTES",
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

    public async Task<MissionRouteResult> GetMissionRouteAsync(
        double originLat,
        double originLng,
        IEnumerable<(double Lat, double Lng)> orderedWaypoints,
        string vehicle = "car",
        CancellationToken cancellationToken = default)
    {
        var points = orderedWaypoints.ToList();
        if (points.Count == 0)
            return new MissionRouteResult { Status = "NO_WAYPOINTS", ErrorMessage = "Không có điểm dừng nào có tọa độ." };

        _logger.LogInformation(
            "Calling Goong Mission Route by segments: origin={OriginLat},{OriginLng} stops={StopCount} vehicle={Vehicle}",
            originLat,
            originLng,
            points.Count,
            vehicle);

        var legs = new List<GoongLegSummary>(points.Count);
        var currentLat = originLat;
        var currentLng = originLng;

        foreach (var point in points)
        {
            var leg = await GetMissionLegAsync(
                currentLat,
                currentLng,
                point.Lat,
                point.Lng,
                vehicle,
                cancellationToken);

            legs.Add(leg);
            currentLat = point.Lat;
            currentLng = point.Lng;
        }

        return new MissionRouteResult
        {
            Status               = BuildMissionRouteStatus(legs),
            ErrorMessage         = BuildMissionRouteErrorMessage(legs),
            TotalDistanceMeters  = legs.Sum(l => l.DistanceMeters),
            TotalDurationSeconds = legs.Sum(l => l.DurationSeconds),
            OverviewPolyline     = BuildOverviewPolyline(legs),
            Legs                 = legs
        };
    }

    private async Task<GoongLegSummary> GetMissionLegAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        string vehicle,
        CancellationToken cancellationToken)
    {
        if (AreSameLocation(fromLat, fromLng, toLat, toLng))
        {
            return new GoongLegSummary
            {
                FromLatitude = fromLat,
                FromLongitude = fromLng,
                ToLatitude = toLat,
                ToLongitude = toLng,
                DistanceMeters = 0,
                DistanceText = FormatDistance(0),
                DurationSeconds = 0,
                DurationText = FormatDuration(0),
                OverviewPolyline = EncodePolyline(new[] { (fromLat, fromLng) }),
                VehicleUsed = vehicle,
                Status = "OK"
            };
        }

        var routeResult = await GetRouteAsync(fromLat, fromLng, toLat, toLng, vehicle, cancellationToken);

        if (routeResult.Status == "OK" && routeResult.Route is not null)
        {
            return new GoongLegSummary
            {
                FromLatitude = fromLat,
                FromLongitude = fromLng,
                ToLatitude = toLat,
                ToLongitude = toLng,
                DistanceMeters = routeResult.Route.TotalDistanceMeters,
                DistanceText = string.IsNullOrWhiteSpace(routeResult.Route.TotalDistanceText)
                    ? FormatDistance(routeResult.Route.TotalDistanceMeters)
                    : routeResult.Route.TotalDistanceText,
                DurationSeconds = routeResult.Route.TotalDurationSeconds,
                DurationText = string.IsNullOrWhiteSpace(routeResult.Route.TotalDurationText)
                    ? FormatDuration(routeResult.Route.TotalDurationSeconds)
                    : routeResult.Route.TotalDurationText,
                OverviewPolyline = string.IsNullOrWhiteSpace(routeResult.Route.OverviewPolyline)
                    ? null
                    : routeResult.Route.OverviewPolyline,
                VehicleUsed = vehicle,
                Status = "OK"
            };
        }

        if (routeResult.Status == "NO_ROUTES")
        {
            return new GoongLegSummary
            {
                FromLatitude = fromLat,
                FromLongitude = fromLng,
                ToLatitude = toLat,
                ToLongitude = toLng,
                DistanceMeters = 0,
                DistanceText = FormatDistance(0),
                DurationSeconds = 0,
                DurationText = FormatDuration(0),
                OverviewPolyline = null,
                VehicleUsed = vehicle,
                Status = "NO_ROUTE",
                ErrorMessage = routeResult.ErrorMessage
            };
        }

        var fallbackDistance = EstimateDirectDistanceMeters(fromLat, fromLng, toLat, toLng);
        var fallbackDuration = EstimateDurationSeconds(fallbackDistance, vehicle);

        return new GoongLegSummary
        {
            FromLatitude = fromLat,
            FromLongitude = fromLng,
            ToLatitude = toLat,
            ToLongitude = toLng,
            DistanceMeters = fallbackDistance,
            DistanceText = FormatDistance(fallbackDistance),
            DurationSeconds = fallbackDuration,
            DurationText = FormatDuration(fallbackDuration),
            OverviewPolyline = EncodePolyline(new[] { (fromLat, fromLng), (toLat, toLng) }),
            VehicleUsed = vehicle,
            Status = "FALLBACK",
            ErrorMessage = string.IsNullOrWhiteSpace(routeResult.ErrorMessage)
                ? "Không thể lấy tuyến đường từ Goong, trả về dữ liệu ước lượng theo đường thẳng."
                : routeResult.ErrorMessage
        };
    }

    private static string BuildMissionRouteStatus(IEnumerable<GoongLegSummary> legs)
    {
        var summaries = legs.ToList();

        if (summaries.Any(leg => leg.Status == "NO_ROUTE"))
            return "NO_ROUTE";

        if (summaries.Any(leg => leg.Status == "FALLBACK"))
            return "FALLBACK";

        return "OK";
    }

    private static string? BuildMissionRouteErrorMessage(IEnumerable<GoongLegSummary> legs)
    {
        var summaries = legs.Where(leg => leg.Status != "OK").ToList();
        if (summaries.Count == 0)
            return null;

        var noRouteCount = summaries.Count(leg => leg.Status == "NO_ROUTE");
        var fallbackCount = summaries.Count(leg => leg.Status == "FALLBACK");

        var parts = new List<string>();
        if (noRouteCount > 0)
            parts.Add($"{noRouteCount} chặng không có tuyến đường");
        if (fallbackCount > 0)
            parts.Add($"{fallbackCount} chặng dùng dữ liệu ước lượng");

        var firstError = summaries.FirstOrDefault(leg => !string.IsNullOrWhiteSpace(leg.ErrorMessage))?.ErrorMessage;
        return firstError is null ? string.Join("; ", parts) : $"{string.Join("; ", parts)}. {firstError}";
    }

    private static string BuildOverviewPolyline(IEnumerable<GoongLegSummary> legs)
    {
        var summaries = legs.ToList();
        if (summaries.Count == 0 || summaries.Any(leg => string.IsNullOrWhiteSpace(leg.OverviewPolyline)))
            return string.Empty;

        var mergedPoints = new List<(double Lat, double Lng)>();

        foreach (var leg in summaries)
        {
            var points = DecodePolyline(leg.OverviewPolyline!);
            if (points.Count == 0)
                return string.Empty;

            if (mergedPoints.Count > 0 && AreSameLocation(
                    mergedPoints[^1].Lat,
                    mergedPoints[^1].Lng,
                    points[0].Lat,
                    points[0].Lng))
            {
                points.RemoveAt(0);
            }

            mergedPoints.AddRange(points);
        }

        return mergedPoints.Count == 0 ? string.Empty : EncodePolyline(mergedPoints);
    }

    private static List<(double Lat, double Lng)> DecodePolyline(string encodedPolyline)
    {
        var points = new List<(double Lat, double Lng)>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encodedPolyline.Length)
        {
            latitude += DecodeNextValue(encodedPolyline, ref index);
            longitude += DecodeNextValue(encodedPolyline, ref index);
            points.Add((latitude / 1E5, longitude / 1E5));
        }

        return points;
    }

    private static string EncodePolyline(IEnumerable<(double Lat, double Lng)> points)
    {
        var builder = new StringBuilder();
        var previousLatitude = 0;
        var previousLongitude = 0;

        foreach (var (lat, lng) in points)
        {
            var latitude = (int)Math.Round(lat * 1E5);
            var longitude = (int)Math.Round(lng * 1E5);

            EncodeValue(latitude - previousLatitude, builder);
            EncodeValue(longitude - previousLongitude, builder);

            previousLatitude = latitude;
            previousLongitude = longitude;
        }

        return builder.ToString();
    }

    private static int DecodeNextValue(string encodedPolyline, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;

        do
        {
            chunk = encodedPolyline[index++] - 63;
            result |= (chunk & 0x1F) << shift;
            shift += 5;
        } while (chunk >= 0x20 && index < encodedPolyline.Length + 1);

        return (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    }

    private static void EncodeValue(int value, StringBuilder builder)
    {
        value <<= 1;
        if (value < 0)
            value = ~value;

        while (value >= 0x20)
        {
            builder.Append((char)((0x20 | (value & 0x1F)) + 63));
            value >>= 5;
        }

        builder.Append((char)(value + 63));
    }

    private static int EstimateDirectDistanceMeters(double fromLat, double fromLng, double toLat, double toLng)
    {
        const double earthRadiusMeters = 6371000;

        var deltaLatitude = DegreesToRadians(toLat - fromLat);
        var deltaLongitude = DegreesToRadians(toLng - fromLng);
        var fromLatitude = DegreesToRadians(fromLat);
        var toLatitude = DegreesToRadians(toLat);

        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
                + Math.Cos(fromLatitude) * Math.Cos(toLatitude)
                * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (int)Math.Round(earthRadiusMeters * c);
    }

    private static int EstimateDurationSeconds(int distanceMeters, string vehicle)
    {
        if (distanceMeters <= 0)
            return 0;

        var metersPerSecond = vehicle.ToLowerInvariant() switch
        {
            "bike" => 4.2,
            "hd" => 6.9,
            "taxi" => 9.7,
            _ => 8.3
        };

        return (int)Math.Ceiling(distanceMeters / metersPerSecond);
    }

    private static string FormatDistance(int distanceMeters)
    {
        if (distanceMeters < 1000)
            return $"{distanceMeters} m";

        var distanceKm = distanceMeters / 1000d;
        return $"{distanceKm.ToString("0.#", CultureInfo.InvariantCulture)} km";
    }

    private static string FormatDuration(int durationSeconds)
    {
        if (durationSeconds < 60)
            return $"{durationSeconds} giây";

        if (durationSeconds < 3600)
            return $"{Math.Max(1, (int)Math.Round(durationSeconds / 60d))} phút";

        var hours = durationSeconds / 3600;
        var minutes = (int)Math.Round((durationSeconds % 3600) / 60d);
        return minutes == 0 ? $"{hours} giờ" : $"{hours} giờ {minutes} phút";
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static bool AreSameLocation(double leftLat, double leftLng, double rightLat, double rightLng)
        => Math.Abs(leftLat - rightLat) < 0.000001 && Math.Abs(leftLng - rightLng) < 0.000001;

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
    }

    // ---- Internal DTOs for Goong API response deserialization ----

    private sealed class GoongDirectionResponse
    {
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
