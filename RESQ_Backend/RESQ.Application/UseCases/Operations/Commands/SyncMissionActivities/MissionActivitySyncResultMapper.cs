using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

internal static class MissionActivitySyncResultMapper
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static MissionActivitySyncResultDto CreateApplied(
        MissionActivitySyncItemDto item,
        MissionActivityStatusExecutionResult executionResult) => new()
        {
            ClientMutationId = item.ClientMutationId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            TargetStatus = item.TargetStatus,
            BaseServerStatus = item.BaseServerStatus,
            Outcome = MissionActivitySyncOutcomes.Applied,
            EffectiveStatus = executionResult.EffectiveStatus,
            CurrentServerStatus = executionResult.CurrentServerStatus,
            ImageUrl = executionResult.ImageUrl,
            ConsumedItems = executionResult.ConsumedItems
        };

    public static MissionActivitySyncResultDto CreateDuplicate(
        MissionActivitySyncItemDto item,
        string? imageUrl,
        string? message = null) => new()
        {
            ClientMutationId = item.ClientMutationId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            TargetStatus = item.TargetStatus,
            BaseServerStatus = item.BaseServerStatus,
            Outcome = MissionActivitySyncOutcomes.Duplicate,
            EffectiveStatus = item.TargetStatus,
            CurrentServerStatus = item.TargetStatus,
            ErrorCode = MissionActivitySyncErrorCodes.AlreadyAtTargetStatus,
            Message = message ?? "Activity hiện đã ở trạng thái mục tiêu.",
            ImageUrl = imageUrl,
            ConsumedItems = []
        };

    public static MissionActivitySyncResultDto CreateConflict(
        MissionActivitySyncItemDto item,
        RESQ.Domain.Enum.Operations.MissionActivityStatus currentServerStatus,
        string? imageUrl,
        string? message = null) => new()
        {
            ClientMutationId = item.ClientMutationId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            TargetStatus = item.TargetStatus,
            BaseServerStatus = item.BaseServerStatus,
            Outcome = MissionActivitySyncOutcomes.Conflict,
            EffectiveStatus = null,
            CurrentServerStatus = currentServerStatus,
            ErrorCode = MissionActivitySyncErrorCodes.BaseStatusMismatch,
            Message = message ?? "Trạng thái hiện tại trên server không khớp với baseServerStatus.",
            ImageUrl = imageUrl,
            ConsumedItems = []
        };

    public static MissionActivitySyncResultDto CreateRejected(
        MissionActivitySyncItemDto item,
        string errorCode,
        string message,
        RESQ.Domain.Enum.Operations.MissionActivityStatus? currentServerStatus,
        string? imageUrl) => new()
        {
            ClientMutationId = item.ClientMutationId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            TargetStatus = item.TargetStatus,
            BaseServerStatus = item.BaseServerStatus,
            Outcome = MissionActivitySyncOutcomes.Rejected,
            EffectiveStatus = null,
            CurrentServerStatus = currentServerStatus,
            ErrorCode = errorCode,
            Message = message,
            ImageUrl = imageUrl,
            ConsumedItems = []
        };

    public static MissionActivitySyncResultDto CreateFailed(
        MissionActivitySyncItemDto item,
        string message,
        RESQ.Domain.Enum.Operations.MissionActivityStatus? currentServerStatus,
        string? imageUrl) => new()
        {
            ClientMutationId = item.ClientMutationId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            TargetStatus = item.TargetStatus,
            BaseServerStatus = item.BaseServerStatus,
            Outcome = MissionActivitySyncOutcomes.Failed,
            EffectiveStatus = null,
            CurrentServerStatus = currentServerStatus,
            ErrorCode = MissionActivitySyncErrorCodes.ServerError,
            Message = message,
            ImageUrl = imageUrl,
            ConsumedItems = []
        };

    public static string SerializeSnapshot(MissionActivitySyncResultDto result) =>
        JsonSerializer.Serialize(result, SnapshotJsonOptions);

    public static MissionActivitySyncResultDto DeserializeSnapshot(string json) =>
        JsonSerializer.Deserialize<MissionActivitySyncResultDto>(json, SnapshotJsonOptions)
        ?? throw new InvalidOperationException("Không thể giải mã response snapshot của mission activity sync.");

    public static MissionActivitySyncSummaryDto BuildSummary(IEnumerable<MissionActivitySyncResultDto> results)
    {
        var items = results.ToList();
        return new MissionActivitySyncSummaryDto
        {
            Total = items.Count,
            Applied = items.Count(item => item.Outcome == MissionActivitySyncOutcomes.Applied),
            Duplicate = items.Count(item => item.Outcome == MissionActivitySyncOutcomes.Duplicate),
            Conflict = items.Count(item => item.Outcome == MissionActivitySyncOutcomes.Conflict),
            Rejected = items.Count(item => item.Outcome == MissionActivitySyncOutcomes.Rejected),
            Failed = items.Count(item => item.Outcome == MissionActivitySyncOutcomes.Failed)
        };
    }
}
