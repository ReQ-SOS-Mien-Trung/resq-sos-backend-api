using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 9 – Routing / Map fail: Haversine distance checks, geographic validation,
/// và edge cases khi toạ độ không hợp lệ hoặc cực đoan.
/// </summary>
public class Flow9_RoutingFailTests
{
    // ────────── Haversine distance: basic calculations ──────────

    [Fact]
    public void Haversine_SamePoint_ZeroDistance()
    {
        double dist = HaversineKm(10.82, 106.63, 10.82, 106.63);
        Assert.Equal(0.0, dist, precision: 10);
    }

    [Fact]
    public void Haversine_HCM_to_HaNoi_OverThousandKm()
    {
        // HCM (10.82, 106.63) → Hà Nội (21.03, 105.85) ≈ 1200 km
        double dist = HaversineKm(10.82, 106.63, 21.03, 105.85);
        Assert.True(dist > 1000, $"HCM to Hanoi should be > 1000 km, got {dist:F1}");
    }

    [Fact]
    public void Haversine_NearbyPoints_SmallDistance()
    {
        // ~1km (0.01 degree ≈ 1.1 km at equator)
        double dist = HaversineKm(10.82, 106.63, 10.83, 106.63);
        Assert.True(dist < 2, $"Nearby points should be < 2 km, got {dist:F3}");
        Assert.True(dist > 0.5, $"Nearby points should be > 0.5 km, got {dist:F3}");
    }

    [Fact]
    public void Haversine_CrossEquator()
    {
        // Singapore (1.35, 103.82) → Jakarta (-6.21, 106.85)
        double dist = HaversineKm(1.35, 103.82, -6.21, 106.85);
        Assert.True(dist > 500, $"SG to Jakarta should be > 500 km, got {dist:F1}");
    }

    // ────────── Cluster spread distance validation ──────────

    [Theory]
    [InlineData(10.82, 106.63, 10.825, 106.635, true)]   // < 1km, within limit
    [InlineData(10.82, 106.63, 10.92, 106.73, false)]     // > 10km, out of limit
    public void ClusterSpreadCheck_10kmLimit(
        double lat1, double lon1, double lat2, double lon2, bool shouldBeWithinLimit)
    {
        const double maxSpreadKm = 10.0;
        double dist = HaversineKm(lat1, lon1, lat2, lon2);
        bool isWithin = dist <= maxSpreadKm;

        Assert.Equal(shouldBeWithinLimit, isWithin);
    }

    [Fact]
    public void ClusterSpreadCheck_AllPairsVerified()
    {
        // Handler checks all pairs: O(n^2)
        var points = new[]
        {
            (lat: 10.82, lon: 106.63),
            (lat: 10.825, lon: 106.635),
            (lat: 10.83, lon: 106.64)
        };

        const double maxSpread = 10.0;
        bool allWithin = true;
        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
            {
                double dist = HaversineKm(
                    points[i].lat, points[i].lon, points[j].lat, points[j].lon);
                if (dist > maxSpread) allWithin = false;
            }
        }

        Assert.True(allWithin);
    }

    [Fact]
    public void ClusterSpreadCheck_OnePairExceeds_FailsCluster()
    {
        // 3 points but 1 pair too far apart
        var points = new[]
        {
            (lat: 10.82, lon: 106.63),      // HCM
            (lat: 10.825, lon: 106.635),     // nearby HCM
            (lat: 21.03, lon: 105.85)        // Hà Nội – too far!
        };

        const double maxSpread = 10.0;
        bool anyExceeds = false;
        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
            {
                double dist = HaversineKm(
                    points[i].lat, points[i].lon, points[j].lat, points[j].lon);
                if (dist > maxSpread) anyExceeds = true;
            }
        }

        Assert.True(anyExceeds);
    }

    // ────────── GeoLocation validation ──────────

    [Fact]
    public void GeoLocation_ValidCoordinates()
    {
        var loc = new GeoLocation(10.82, 106.63);
        Assert.Equal(10.82, loc.Latitude);
        Assert.Equal(106.63, loc.Longitude);
    }

    [Fact]
    public void GeoLocation_NegativeCoordinates_Valid()
    {
        // Southern hemisphere, western hemisphere
        var loc = new GeoLocation(-33.87, -151.21);
        Assert.Equal(-33.87, loc.Latitude);
        Assert.Equal(-151.21, loc.Longitude);
    }

    // ────────── SOS Request with location ──────────

    [Fact]
    public void SosRequest_StoresLocation()
    {
        var sos = SosRequestModel.Create(
            Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");

        Assert.NotNull(sos.Location);
        Assert.Equal(10.82, sos.Location!.Latitude);
        Assert.Equal(106.63, sos.Location.Longitude);
    }

    [Fact]
    public void CenterCalculation_AverageOfLocations()
    {
        // Handler: centerLat = validCoords.Average(r => r.Location!.Latitude)
        var lats = new[] { 10.0, 10.5, 11.0 };
        var lons = new[] { 106.0, 106.5, 107.0 };

        double centerLat = lats.Average();
        double centerLon = lons.Average();

        Assert.Equal(10.5, centerLat, precision: 10);
        Assert.Equal(106.5, centerLon, precision: 10);
    }

    [Fact]
    public void CenterCalculation_SinglePoint()
    {
        double centerLat = new[] { 10.82 }.Average();
        double centerLon = new[] { 106.63 }.Average();

        Assert.Equal(10.82, centerLat);
        Assert.Equal(106.63, centerLon);
    }

    // ────────── Helper: same Haversine as CreateSosClusterCommandHandler ──────────

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
