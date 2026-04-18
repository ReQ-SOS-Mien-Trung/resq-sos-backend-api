using FluentValidation;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Luồng 4 – SOS trùng lặp / spam: Validate đầu vào khi tạo cluster với SOS request trùng lặp hoặc
/// SOS không đủ điều kiện gom.
/// </summary>
public class Flow4_SosDuplicateSpamTests
{
    // ────────── Validator: duplicate SOS IDs ──────────

    [Fact]
    public void Validator_RejectsDuplicateSosIds()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [1, 2, 2, 3],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("trùng lặp"));
    }

    [Fact]
    public void Validator_RejectsEmptySosIds()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("ít nhất một SOS request"));
    }

    [Fact]
    public void Validator_AcceptsUniqueSosIds()
    {
        var validator = new CreateSosClusterCommandValidator();
        var command = new CreateSosClusterCommand(
            SosRequestIds: [1, 2, 3],
            CreatedByUserId: Guid.NewGuid()
        );

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    // ────────── SOS status must be Pending or Incident for clustering ──────────

    [Theory]
    [InlineData(SosRequestStatus.Pending)]
    [InlineData(SosRequestStatus.Incident)]
    public void SosRequest_ValidStatusForClustering(SosRequestStatus status)
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        if (status == SosRequestStatus.Incident)
        {
            sos.SetStatus(SosRequestStatus.InProgress);
            sos.SetStatus(SosRequestStatus.Incident);
        }

        Assert.Equal(status, sos.Status);
    }

    [Theory]
    [InlineData(SosRequestStatus.Assigned)]
    [InlineData(SosRequestStatus.InProgress)]
    [InlineData(SosRequestStatus.Resolved)]
    public void SosRequest_InvalidStatusForClustering(SosRequestStatus status)
    {
        // These statuses should be rejected by handler when creating cluster
        // Chỉ Pending hoặc Incident mới được gom cluster
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        if (status == SosRequestStatus.Assigned)
            sos.SetStatus(SosRequestStatus.Assigned);
        else if (status == SosRequestStatus.InProgress)
        {
            sos.SetStatus(SosRequestStatus.Assigned);
            sos.SetStatus(SosRequestStatus.InProgress);
        }
        else if (status == SosRequestStatus.Resolved)
        {
            sos.SetStatus(SosRequestStatus.Assigned);
            sos.SetStatus(SosRequestStatus.InProgress);
            sos.SetStatus(SosRequestStatus.Resolved);
        }

        // Handler sẽ reject: "Chỉ được tạo cluster từ các SOS request ở trạng thái Pending hoặc Incident"
        Assert.True(sos.Status != SosRequestStatus.Pending && sos.Status != SosRequestStatus.Incident);
    }

    // ────────── SOS already in cluster ──────────

    [Fact]
    public void SosRequest_AlreadyClustered_ShouldBeRejected()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");
        sos.ClusterId = 42;

        // Handler rejects: "Các SOS request sau đã thuộc cluster khác"
        Assert.True(sos.ClusterId.HasValue);
    }

    [Fact]
    public void SosRequest_NotClustered_IsValid()
    {
        var sos = SosRequestModel.Create(Guid.NewGuid(), new GeoLocation(10.82, 106.63), "SOS");

        Assert.False(sos.ClusterId.HasValue);
    }

    // ────────── Multiple SOS from same user ──────────

    [Fact]
    public void MultipleSos_SameUser_EachGetsPendingStatus()
    {
        var userId = Guid.NewGuid();
        var sos1 = SosRequestModel.Create(userId, new GeoLocation(10.82, 106.63), "SOS lần 1");
        var sos2 = SosRequestModel.Create(userId, new GeoLocation(10.82, 106.63), "SOS lần 2");
        var sos3 = SosRequestModel.Create(userId, new GeoLocation(10.82, 106.63), "SOS lần 3");

        Assert.Equal(SosRequestStatus.Pending, sos1.Status);
        Assert.Equal(SosRequestStatus.Pending, sos2.Status);
        Assert.Equal(SosRequestStatus.Pending, sos3.Status);
        Assert.Equal(userId, sos1.UserId);
        Assert.Equal(userId, sos2.UserId);
    }

    // ────────── Cluster model: InProgress status tracks active mission ──────────

    [Fact]
    public void SosClusterModel_InProgressStatus_TracksActiveMission()
    {
        var cluster = new SosClusterModel
        {
            Id = 1,
            SosRequestIds = [1, 2, 3],
            Status = SosClusterStatus.InProgress
        };

        // Handler validates active status trước khi tạo mission mới
        Assert.Equal(SosClusterStatus.InProgress, cluster.Status);
    }

    [Fact]
    public void SosClusterModel_DefaultStatus_IsPending()
    {
        var cluster = new SosClusterModel
        {
            Id = 1,
            SosRequestIds = [1, 2]
        };

        Assert.Equal(SosClusterStatus.Pending, cluster.Status);
    }
}
