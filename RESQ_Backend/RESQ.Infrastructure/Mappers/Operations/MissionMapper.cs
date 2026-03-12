using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionMapper
{
    private static readonly Dictionary<MissionStatus, string> StatusToString = new()
    {
        [MissionStatus.Planned] = "planned",
        [MissionStatus.OnGoing] = "on_going",
        [MissionStatus.Completed] = "completed",
        [MissionStatus.Incompleted] = "incompleted"
    };

    private static readonly Dictionary<string, MissionStatus> StringToStatus =
        StatusToString.ToDictionary(x => x.Value, x => x.Key);

    public static string ToDbString(MissionStatus status) =>
        StatusToString.GetValueOrDefault(status, "planned");

    public static MissionStatus ToEnum(string? status) =>
        status is not null && StringToStatus.TryGetValue(status, out var val) ? val : MissionStatus.Planned;

    public static Mission ToEntity(MissionModel model)
    {
        return new Mission
        {
            ClusterId = model.ClusterId,
            PreviousMissionId = model.PreviousMissionId,
            MissionType = model.MissionType,
            PriorityScore = model.PriorityScore,
            Status = ToDbString(model.Status),
            StartTime = model.StartTime,
            ExpectedEndTime = model.ExpectedEndTime,
            IsCompleted = model.IsCompleted ?? false,
            CreatedById = model.CreatedById,
            CreatedAt = model.CreatedAt ?? DateTime.UtcNow,
            CompletedAt = model.CompletedAt
        };
    }

    public static MissionModel ToDomain(Mission entity)
    {
        return new MissionModel
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            PreviousMissionId = entity.PreviousMissionId,
            MissionType = entity.MissionType,
            PriorityScore = entity.PriorityScore,
            Status = ToEnum(entity.Status),
            StartTime = entity.StartTime,
            ExpectedEndTime = entity.ExpectedEndTime,
            IsCompleted = entity.IsCompleted,
            CreatedById = entity.CreatedById,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt,
            Activities = entity.MissionActivities
                .Select(MissionActivityMapper.ToDomain)
                .ToList()
        };
    }
}
