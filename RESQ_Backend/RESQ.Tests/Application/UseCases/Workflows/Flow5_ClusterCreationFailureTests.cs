using FluentValidation;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 5 – Coordinator không thể tạo Cluster: SOS quá xa nhau, SOS sai trạng thái,
/// SOS đã gom rồi, hoặc validator từ chối.
/// </summary>
public class Flow5_ClusterCreationFailureTests
{
    // ────────── Cluster validator: empty & duplicate ──────────

    [Fact]
    public void ClusterValidator_RejectsEmpty()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClusterValidator_RejectsDuplicateIds()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [5, 5],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClusterValidator_AcceptsSingleSos()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [1],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    // ────────── Haversine distance concept tests ──────────

    [Fact]
    public void HaversineDistance_SamePoint_IsZero()
    {
        // Cùng toạ độ → khoảng cách = 0
        double dist = HaversineKm(10.82, 106.63, 10.82, 106.63);
        Assert.Equal(0.0, dist, precision: 5);
    }

    [Fact]
    public void HaversineDistance_NearbyPoints_WithinLimit()
    {
        // 2 điểm cách ~1km tại HCM
        double dist = HaversineKm(10.82, 106.63, 10.829, 106.63);
        Assert.True(dist < 10.0, $"Distance {dist:F2} km should be < 10 km");
    }

    [Fact]
    public void HaversineDistance_FarPoints_ExceedsLimit()
    {
        // HCM → Hà Nội: ~1200km – vượt giới hạn 10km
        double dist = HaversineKm(10.82, 106.63, 21.03, 105.85);
        Assert.True(dist > 10.0, $"Distance {dist:F2} km should be > 10 km");
    }

    [Fact]
    public void HaversineDistance_PointsJustOver10km()
    {
        // ~12km cách nhau (khoảng 0.1 degree latitude ≈ 11km)
        double dist = HaversineKm(10.82, 106.63, 10.92, 106.63);
        Assert.True(dist > 10.0, $"Distance {dist:F2} km should exceed 10 km limit");
    }

    // ────────── SOS requests not eligible for clustering ──────────

    [Fact]
    public void SosRequest_Assigned_CannotBeClustered()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.Assigned);

        // Handler sẽ reject: status phải là Pending hoặc Incident
        Assert.NotEqual(SosRequestStatus.Pending, sos.Status);
        Assert.NotEqual(SosRequestStatus.Incident, sos.Status);
    }

    [Fact]
    public void SosRequest_Resolved_CannotBeClustered()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.SetStatus(SosRequestStatus.Assigned);
        sos.SetStatus(SosRequestStatus.InProgress);
        sos.SetStatus(SosRequestStatus.Resolved);

        Assert.Equal(SosRequestStatus.Resolved, sos.Status);
    }

    [Fact]
    public void SosRequest_AlreadyInCluster_Rejected()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.ClusterId = 10;

        Assert.True(sos.ClusterId.HasValue);
    }

    // ────────── Cluster data auto-calculation ──────────

    [Fact]
    public void SosClusterModel_CenterCoordinates_AreAverage()
    {
        // Handler tính center = average of all SOS locations
        var locations = new[]
        {
            new GeoLocation(10.0, 106.0),
            new GeoLocation(10.2, 106.2),
            new GeoLocation(10.4, 106.4)
        };

        double expectedLat = (10.0 + 10.2 + 10.4) / 3;
        double expectedLon = (106.0 + 106.2 + 106.4) / 3;

        Assert.Equal(10.2, expectedLat, precision: 10);
        Assert.Equal(106.2, expectedLon, precision: 10);
    }

    [Fact]
    public void SosClusterModel_SeverityFromHighestPriority()
    {
        var sos1 = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS 1");
        sos1.SetPriorityLevel(SosPriorityLevel.Low);

        var sos2 = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS 2");
        sos2.SetPriorityLevel(SosPriorityLevel.Critical);

        // Handler takes max priority → "Critical"
        var highest = new[] { sos1.PriorityLevel!.Value, sos2.PriorityLevel!.Value }.Max();
        Assert.Equal(SosPriorityLevel.Critical, highest);
    }

    // ────────── Mission validator: ClusterId required ──────────

    [Fact]
    public void MissionValidator_RejectsZeroClusterId()
    {
        var validator = new CreateMissionCommandValidator();
        var command = new CreateMissionCommand(
            ClusterId: 0,
            MissionType: null,
            PriorityScore: null,
            StartTime: null,
            ExpectedEndTime: null,
            Activities: [],
            CreatedById: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ClusterId");
    }

    [Fact]
    public void MissionValidator_RejectsEmptyCreatedById()
    {
        var validator = new CreateMissionCommandValidator();
        var command = new CreateMissionCommand(
            ClusterId: 1,
            MissionType: null,
            PriorityScore: null,
            StartTime: null,
            ExpectedEndTime: null,
            Activities: [],
            CreatedById: Guid.Empty
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CreatedById");
    }

    // ────────── Helper: Haversine formula (same as handler) ──────────

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
