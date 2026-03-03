using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionById;

public class GetMissionByIdQueryHandler(
    IMissionRepository missionRepository,
    ILogger<GetMissionByIdQueryHandler> logger
) : IRequestHandler<GetMissionByIdQuery, MissionDto?>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly ILogger<GetMissionByIdQueryHandler> _logger = logger;

    public async Task<MissionDto?> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null) return null;

        return new MissionDto
        {
            Id = mission.Id,
            ClusterId = mission.ClusterId,
            MissionType = mission.MissionType,
            PriorityScore = mission.PriorityScore,
            Status = mission.Status,
            StartTime = mission.StartTime,
            ExpectedEndTime = mission.ExpectedEndTime,
            IsCompleted = mission.IsCompleted,
            CreatedById = mission.CreatedById,
            CreatedAt = mission.CreatedAt,
            CompletedAt = mission.CompletedAt,
            ActivityCount = mission.Activities.Count,
            Activities = mission.Activities.Select(a => new MissionActivityDto
            {
                Id = a.Id,
                Step = a.Step,
                ActivityCode = a.ActivityCode,
                ActivityType = a.ActivityType,
                Description = a.Description,
                Target = a.Target,
                Items = a.Items,
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = a.Status,
                AssignedAt = a.AssignedAt,
                CompletedAt = a.CompletedAt
            }).ToList()
        };
    }
}
