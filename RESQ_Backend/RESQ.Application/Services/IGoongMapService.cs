namespace RESQ.Application.Services;

public interface IGoongMapService
{
    /// <summary>
    /// Lấy tuyến đường từ vị trí hiện tại của rescuer đến địa điểm đích trong mission activity.
    /// </summary>
    Task<GoongRouteResult> GetRouteAsync(
        double originLat,
        double originLng,
        double destLat,
        double destLng,
        string vehicle = "car",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tuyến đường qua nhiều điểm dừng (origin → waypoint1 → ... → destination).
    /// Trả về danh sách legs tương ứng với từng đoạn.
    /// </summary>
    Task<MissionRouteResult> GetMissionRouteAsync(
        double originLat,
        double originLng,
        IEnumerable<(double Lat, double Lng)> orderedWaypoints,
        string vehicle = "car",
        CancellationToken cancellationToken = default);
}

public class GoongRouteResult
{
    public string Status { get; set; } = string.Empty;
    public GoongRouteSummary? Route { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GoongRouteSummary
{
    /// <summary>Tổng khoảng cách (mét)</summary>
    public int TotalDistanceMeters { get; set; }

    /// <summary>Tổng khoảng cách dưới dạng văn bản (vd: "3.2 km")</summary>
    public string TotalDistanceText { get; set; } = string.Empty;

    /// <summary>Tổng thời gian di chuyển (giây)</summary>
    public int TotalDurationSeconds { get; set; }

    /// <summary>Tổng thời gian di chuyển dưới dạng văn bản (vd: "12 phút")</summary>
    public string TotalDurationText { get; set; } = string.Empty;

    /// <summary>Polyline mã hoá toàn tuyến để vẽ trên bản đồ</summary>
    public string OverviewPolyline { get; set; } = string.Empty;

    /// <summary>Tóm tắt tên đường chính</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Danh sách các bước chỉ đường</summary>
    public List<GoongRouteStep> Steps { get; set; } = [];
}

public class GoongRouteStep
{
    public string Instruction { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public string DistanceText { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string DurationText { get; set; } = string.Empty;
    public string Maneuver { get; set; } = string.Empty;
    public double StartLat { get; set; }
    public double StartLng { get; set; }
    public double EndLat { get; set; }
    public double EndLng { get; set; }
    /// <summary>Polyline mã hoá của bước này</summary>
    public string Polyline { get; set; } = string.Empty;
}

// ── Multi-waypoint result ─────────────────────────────────────────────────────

public class MissionRouteResult
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalDistanceMeters { get; set; }
    public int TotalDurationSeconds { get; set; }
    /// <summary>Polyline toàn tuyến từ Goong (encoded)</summary>
    public string OverviewPolyline { get; set; } = string.Empty;
    /// <summary>Mỗi phần tử = 1 đoạn: origin→wp[0], wp[0]→wp[1], ..., wp[n-2]→wp[n-1].</summary>
    public List<GoongLegSummary> Legs { get; set; } = [];
}

public class GoongLegSummary
{
    public int SegmentIndex { get; set; }
    public int? FromStep { get; set; }
    public int? ToStep { get; set; }
    public double FromLatitude { get; set; }
    public double FromLongitude { get; set; }
    public double ToLatitude { get; set; }
    public double ToLongitude { get; set; }
    public string OverviewPolyline { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public string DistanceText { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string DurationText { get; set; } = string.Empty;
}
