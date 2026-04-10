using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Emergency.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class SosRequestModelTests
{
    private static readonly GeoLocation ValidLocation = new(10.762622, 106.660172);

    // ── Create factory ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithEmptyUserId_ThrowsInvalidUserException()
    {
        Assert.Throws<InvalidSosRequestUserException>(() =>
            SosRequestModel.Create(Guid.Empty, ValidLocation, "Cần cứu trợ"));
    }

    [Fact]
    public void Create_WithEmptyRawMessage_ThrowsInvalidMessageException()
    {
        Assert.Throws<InvalidSosRequestMessageException>(() =>
            SosRequestModel.Create(Guid.NewGuid(), ValidLocation, ""));
    }

    [Fact]
    public void Create_WithWhitespaceRawMessage_ThrowsInvalidMessageException()
    {
        Assert.Throws<InvalidSosRequestMessageException>(() =>
            SosRequestModel.Create(Guid.NewGuid(), ValidLocation, "   "));
    }

    [Fact]
    public void Create_WithValidArguments_ReturnsPendingModel()
    {
        var userId = Guid.NewGuid();

        var model = SosRequestModel.Create(userId, ValidLocation, "Cần cứu trợ khẩn cấp");

        Assert.Equal(userId, model.UserId);
        Assert.Equal(SosRequestStatus.Pending, model.Status);
        Assert.Equal(ValidLocation, model.Location);
    }

    [Fact]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromRawMessage()
    {
        var model = SosRequestModel.Create(Guid.NewGuid(), ValidLocation, "  lũ lụt  ");

        Assert.Equal("lũ lụt", model.RawMessage);
    }

    [Fact]
    public void Create_UsesClientCreatedAt_WhenProvided()
    {
        var clientTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var model = SosRequestModel.Create(
            Guid.NewGuid(), ValidLocation, "Cần cứu trợ",
            clientCreatedAt: clientTime);

        Assert.Equal(clientTime, model.CreatedAt);
    }

    [Fact]
    public void Create_SetsReceivedAtToServerTime_EvenWhenClientCreatedAtIsProvided()
    {
        var clientTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = DateTime.UtcNow;

        var model = SosRequestModel.Create(
            Guid.NewGuid(), ValidLocation, "Lũ lớn",
            clientCreatedAt: clientTime);

        Assert.True(model.ReceivedAt >= before);
    }

    // ── SetPriorityLevel / SetStatus helpers ────────────────────────────────────

    [Fact]
    public void SetPriorityLevel_UpdatesPriorityLevel()
    {
        var model = SosRequestModel.Create(Guid.NewGuid(), ValidLocation, "Test");

        model.SetPriorityLevel(SosPriorityLevel.Critical);

        Assert.Equal(SosPriorityLevel.Critical, model.PriorityLevel);
    }

    [Fact]
    public void SetStatus_UpdatesStatus()
    {
        var model = SosRequestModel.Create(Guid.NewGuid(), ValidLocation, "Test");

        model.SetStatus(SosRequestStatus.Assigned);

        Assert.Equal(SosRequestStatus.Assigned, model.Status);
    }
}
