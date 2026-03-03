using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionActivityMapper
{
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
            Status = model.Status ?? "pending",
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
            Status = entity.Status,
            AssignedAt = entity.AssignedAt,
            CompletedAt = entity.CompletedAt,
            LastDecisionBy = entity.LastDecisionBy
        };
    }
}
