using System.Globalization;
using System.Net.Http.Json;
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
        var routeResponse = await GetDirectionRouteAsync(originLat, originLng, destLat, destLng, vehicle, cancellationToken);
        if (routeResponse.Status != "OK" || routeResponse.Route is null || routeResponse.PrimaryLeg is null)
        {
            return new GoongRouteResult
            {
                Status = routeResponse.Status,
                ErrorMessage = routeResponse.ErrorMessage
            };
        }

        var bestRoute = routeResponse.Route;
        var leg = routeResponse.PrimaryLeg;
        var overviewPolyline = BuildSegmentPolyline(bestRoute, leg, (originLat, originLng), (destLat, destLng));

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
                OverviewPolyline = overviewPolyline,
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

        var routePoints = new List<(double Lat, double Lng)>(points.Count + 1)
        {
            (originLat, originLng)
        };
        routePoints.AddRange(points);

        var aggregatedPath = new List<(double Lat, double Lng)>();
        var legs = new List<GoongLegSummary>(points.Count);

        for (var index = 1; index < routePoints.Count; index++)
        {
            var from = routePoints[index - 1];
            var to = routePoints[index];

            if (AreSameCoordinates(from, to))
            {
                var stationaryPath = new List<(double Lat, double Lng)> { from, to };
                AppendRoutePoints(aggregatedPath, stationaryPath);

                legs.Add(new GoongLegSummary
                {
                    SegmentIndex = index - 1,
                    FromLatitude = from.Lat,
                    FromLongitude = from.Lng,
                    ToLatitude = to.Lat,
                    ToLongitude = to.Lng,
                    OverviewPolyline = EncodePolyline(stationaryPath)
                });

                continue;
            }

            var segmentResponse = await GetDirectionRouteAsync(from.Lat, from.Lng, to.Lat, to.Lng, vehicle, cancellationToken);
            if (segmentResponse.Status != "OK" || segmentResponse.Route is null || segmentResponse.PrimaryLeg is null)
            {
                _logger.LogWarning(
                    "Failed to calculate mission route segment {SegmentIndex} from ({FromLat}, {FromLng}) to ({ToLat}, {ToLng}). Status={Status}, Error={Error}",
                    index - 1,
                    from.Lat,
                    from.Lng,
                    to.Lat,
                    to.Lng,
                    segmentResponse.Status,
                    segmentResponse.ErrorMessage);

                return new MissionRouteResult
                {
                    Status = segmentResponse.Status,
                    ErrorMessage = segmentResponse.ErrorMessage
                };
            }

            var segmentPolyline = BuildSegmentPolyline(segmentResponse.Route, segmentResponse.PrimaryLeg, from, to);
            var segmentPath = DecodePolyline(segmentPolyline);
            if (segmentPath.Count == 0)
                segmentPath.AddRange([from, to]);

            AppendRoutePoints(aggregatedPath, segmentPath);

            legs.Add(new GoongLegSummary
            {
                SegmentIndex = index - 1,
                FromLatitude = from.Lat,
                FromLongitude = from.Lng,
                ToLatitude = to.Lat,
                ToLongitude = to.Lng,
                OverviewPolyline = segmentPolyline,
                DistanceMeters = segmentResponse.PrimaryLeg.Distance?.Value ?? 0,
                DistanceText = segmentResponse.PrimaryLeg.Distance?.Text ?? string.Empty,
                DurationSeconds = segmentResponse.PrimaryLeg.Duration?.Value ?? 0,
                DurationText = segmentResponse.PrimaryLeg.Duration?.Text ?? string.Empty
            });
        }

        return new MissionRouteResult
        {
            Status               = "OK",
            TotalDistanceMeters  = legs.Sum(l => l.DistanceMeters),
            TotalDurationSeconds = legs.Sum(l => l.DurationSeconds),
            OverviewPolyline     = EncodePolyline(aggregatedPath),
            Legs                 = legs
        };
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
    }

    private async Task<DirectionRouteResponse> GetDirectionRouteAsync(
        double originLat,
        double originLng,
        double destLat,
        double destLng,
        string vehicle,
        CancellationToken cancellationToken)
    {
        var origin = FormatCoordinate(originLat, originLng);
        var destination = FormatCoordinate(destLat, destLng);
        var url = $"{_baseUrl}/Direction?origin={origin}&destination={destination}&vehicle={vehicle}&api_key={_apiKey}";

        _logger.LogInformation(
            "Calling Goong Direction API: origin={Origin} dest={Destination} vehicle={Vehicle}",
            origin,
            destination,
            vehicle);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Goong Direction API call failed");
            return new DirectionRouteResponse { Status = "ERROR", ErrorMessage = ex.Message };
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Goong API returned HTTP {StatusCode}: {Body}", response.StatusCode, body);
            return new DirectionRouteResponse
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
            return new DirectionRouteResponse { Status = "ERROR", ErrorMessage = "Không thể đọc kết quả từ Goong API." };
        }

        if (goongResp?.Routes is null || goongResp.Routes.Count == 0)
        {
            return new DirectionRouteResponse
            {
                Status = "NO_ROUTES",
                ErrorMessage = "Goong API không trả về tuyến đường."
            };
        }

        var bestRoute = goongResp.Routes[0];
        var primaryLeg = bestRoute.Legs?.FirstOrDefault();
        if (primaryLeg is null)
        {
            return new DirectionRouteResponse
            {
                Status = "NO_ROUTES",
                ErrorMessage = "Goong API không trả về chặng đường hợp lệ."
            };
        }

        return new DirectionRouteResponse
        {
            Status = "OK",
            Route = bestRoute,
            PrimaryLeg = primaryLeg
        };
    }

    private static string FormatCoordinate(double latitude, double longitude)
    {
        var fmt = CultureInfo.InvariantCulture;
        return $"{latitude.ToString(fmt)},{longitude.ToString(fmt)}";
    }

    private static string BuildSegmentPolyline(
        GoongRoute route,
        GoongLeg leg,
        (double Lat, double Lng) from,
        (double Lat, double Lng) to)
    {
        var routePoints = DecodePolyline(route.OverviewPolyline?.Points ?? string.Empty);
        if (routePoints.Count > 0)
            return EncodePolyline(routePoints);

        var stepPoints = new List<(double Lat, double Lng)>();
        foreach (var step in leg.Steps ?? [])
        {
            var decodedStep = DecodePolyline(step.Polyline?.Points ?? string.Empty);
            AppendRoutePoints(stepPoints, decodedStep);
        }

        if (stepPoints.Count > 0)
            return EncodePolyline(stepPoints);

        return EncodePolyline([from, to]);
    }

    private static bool AreSameCoordinates((double Lat, double Lng) from, (double Lat, double Lng) to) =>
        Math.Abs(from.Lat - to.Lat) < 0.000001d
        && Math.Abs(from.Lng - to.Lng) < 0.000001d;

    private static void AppendRoutePoints(
        List<(double Lat, double Lng)> destination,
        IEnumerable<(double Lat, double Lng)> segment)
    {
        foreach (var point in segment)
        {
            if (destination.Count > 0 && AreSameCoordinates(destination[^1], point))
                continue;

            destination.Add(point);
        }
    }

    private static string EncodePolyline(IEnumerable<(double Lat, double Lng)> points)
    {
        var builder = new StringBuilder();
        var previousLat = 0;
        var previousLng = 0;

        foreach (var (lat, lng) in points)
        {
            var currentLat = (int)Math.Round(lat * 1e5);
            var currentLng = (int)Math.Round(lng * 1e5);

            EncodePolylineValue(currentLat - previousLat, builder);
            EncodePolylineValue(currentLng - previousLng, builder);

            previousLat = currentLat;
            previousLng = currentLng;
        }

        return builder.ToString();
    }

    private static void EncodePolylineValue(int value, StringBuilder builder)
    {
        value <<= 1;
        if (value < 0)
            value = ~value;

        while (value >= 0x20)
        {
            builder.Append((char)((0x20 | (value & 0x1f)) + 63));
            value >>= 5;
        }

        builder.Append((char)(value + 63));
    }

    private static List<(double Lat, double Lng)> DecodePolyline(string encodedPolyline)
    {
        var points = new List<(double Lat, double Lng)>();
        if (string.IsNullOrWhiteSpace(encodedPolyline))
            return points;

        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encodedPolyline.Length)
        {
            latitude += DecodePolylineValue(encodedPolyline, ref index);
            longitude += DecodePolylineValue(encodedPolyline, ref index);
            points.Add((latitude / 1e5, longitude / 1e5));
        }

        return points;
    }

    private static int DecodePolylineValue(string encodedPolyline, ref int index)
    {
        var shift = 0;
        var result = 0;

        while (index < encodedPolyline.Length)
        {
            var chunk = encodedPolyline[index++] - 63;
            result |= (chunk & 0x1f) << shift;
            shift += 5;

            if (chunk < 0x20)
                break;
        }

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    // ---- Internal DTOs for Goong API response deserialization ----

    private sealed class DirectionRouteResponse
    {
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public GoongRoute? Route { get; set; }
        public GoongLeg? PrimaryLeg { get; set; }
    }

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
