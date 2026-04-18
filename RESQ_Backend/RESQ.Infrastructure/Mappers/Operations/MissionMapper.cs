using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionMapper
{
    private static readonly Dictionary<MissionStatus, string> StatusToString = new()
    {
        [MissionStatus.Planned] = nameof(MissionStatus.Planned),
        [MissionStatus.OnGoing] = nameof(MissionStatus.OnGoing),
        [MissionStatus.Completed] = nameof(MissionStatus.Completed),
        [MissionStatus.Incompleted] = nameof(MissionStatus.Incompleted)
    };

    private static readonly Dictionary<string, MissionStatus> StringToStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        [NormalizeStatusKey(nameof(MissionStatus.Planned))] = MissionStatus.Planned,
        [NormalizeStatusKey("planned")] = MissionStatus.Planned,
        [NormalizeStatusKey(nameof(MissionStatus.OnGoing))] = MissionStatus.OnGoing,
        [NormalizeStatusKey("on_going")] = MissionStatus.OnGoing,
        [NormalizeStatusKey(nameof(MissionStatus.Completed))] = MissionStatus.Completed,
        [NormalizeStatusKey("completed")] = MissionStatus.Completed,
        [NormalizeStatusKey(nameof(MissionStatus.Incompleted))] = MissionStatus.Incompleted,
        [NormalizeStatusKey("incompleted")] = MissionStatus.Incompleted
    };

    public static string ToDbString(MissionStatus status) =>
        StatusToString.GetValueOrDefault(status, nameof(MissionStatus.Planned));

    public static MissionStatus ToEnum(string? status) =>
        status is not null && StringToStatus.TryGetValue(NormalizeStatusKey(status.Trim()), out var val)
            ? val
            : MissionStatus.Planned;

    private static string NormalizeStatusKey(string status) =>
        status.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    public static Mission ToEntity(MissionModel model)
    {
        return new Mission
        {
            ClusterId = model.ClusterId,
            PreviousMissionId = model.PreviousMissionId,
            AiSuggestionId = model.AiSuggestionId,
            MissionType = model.MissionType,
            PriorityScore = model.PriorityScore,
            Status = ToDbString(model.Status),
            StartTime = model.StartTime,
            ExpectedEndTime = model.ExpectedEndTime,
            IsCompleted = model.IsCompleted ?? false,
            CreatedById = model.CreatedById,
            CreatedAt = model.CreatedAt ?? DateTime.UtcNow,
            CompletedAt = model.CompletedAt,
            ManualOverrideMetadata = model.ManualOverrideMetadata
        };
    }

    public static MissionModel ToDomain(Mission entity)
    {
        return new MissionModel
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            PreviousMissionId = entity.PreviousMissionId,
            AiSuggestionId = entity.AiSuggestionId,
            MissionType = entity.MissionType,
            PriorityScore = entity.PriorityScore,
            Status = ToEnum(entity.Status),
            StartTime = entity.StartTime,
            ExpectedEndTime = entity.ExpectedEndTime,
            IsCompleted = entity.IsCompleted,
            CreatedById = entity.CreatedById,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt,
            ManualOverrideMetadata = entity.ManualOverrideMetadata,
            Activities = entity.MissionActivities
                .Select(MissionActivityMapper.ToDomain)
                .ToList()
        };
    }
}
