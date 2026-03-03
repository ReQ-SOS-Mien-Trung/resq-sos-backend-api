using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Mappers.Operations;

public static class MissionMapper
{
    public static Mission ToEntity(MissionModel model)
    {
        return new Mission
        {
            ClusterId = model.ClusterId,
            PreviousMissionId = model.PreviousMissionId,
            MissionType = model.MissionType,
            PriorityScore = model.PriorityScore,
            Status = model.Status ?? "pending",
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
            Status = entity.Status,
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
