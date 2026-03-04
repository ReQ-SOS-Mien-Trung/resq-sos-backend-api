using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionActivityMapper
{
    private static readonly Dictionary<MissionActivityStatus, string> StatusToString = new()
    {
        [MissionActivityStatus.Pending] = "pending",
        [MissionActivityStatus.InProgress] = "in_progress",
        [MissionActivityStatus.Completed] = "completed",
        [MissionActivityStatus.Cancelled] = "cancelled"
    };

    private static readonly Dictionary<string, MissionActivityStatus> StringToStatus =
        StatusToString.ToDictionary(x => x.Value, x => x.Key);

    public static string ToDbString(MissionActivityStatus status) =>
        StatusToString.GetValueOrDefault(status, "pending");

    public static MissionActivityStatus ToEnum(string? status) =>
        status is not null && StringToStatus.TryGetValue(status, out var val) ? val : MissionActivityStatus.Pending;

    public static MissionActivity ToEntity(MissionActivityModel model)
    {
        var entity = new MissionActivity
        {
            MissionId = model.MissionId,
            Step = model.Step,
            ActivityCode = model.ActivityCode,
            ActivityType = model.ActivityType,
            Description = model.Description,
            Target = model.Target,
            Items = model.Items,
            Status = ToDbString(model.Status),
            AssignedAt = model.AssignedAt,
            CompletedAt = model.CompletedAt,
            LastDecisionBy = model.LastDecisionBy
        };

        if (model.TargetLatitude.HasValue && model.TargetLongitude.HasValue)
        {
            entity.TargetLocation = new Point(model.TargetLongitude.Value, model.TargetLatitude.Value) { SRID = 4326 };
        }

        return entity;
    }

    public static MissionActivityModel ToDomain(MissionActivity entity)
    {
        return new MissionActivityModel
        {
            Id = entity.Id,
            MissionId = entity.MissionId,
            Step = entity.Step,
            ActivityCode = entity.ActivityCode,
            ActivityType = entity.ActivityType,
            Description = entity.Description,
            Target = entity.Target,
            Items = entity.Items,
            TargetLatitude = entity.TargetLocation?.Y,
            TargetLongitude = entity.TargetLocation?.X,
            Status = ToEnum(entity.Status),
            AssignedAt = entity.AssignedAt,
            CompletedAt = entity.CompletedAt,
            LastDecisionBy = entity.LastDecisionBy
        };
    }
}
