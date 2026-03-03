using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public class GetMissionsQueryHandler(
    IMissionRepository missionRepository,
    ILogger<GetMissionsQueryHandler> logger
) : IRequestHandler<GetMissionsQuery, GetMissionsResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly ILogger<GetMissionsQueryHandler> _logger = logger;

    public async Task<GetMissionsResponse> Handle(GetMissionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting missions, ClusterId={clusterId}", request.ClusterId);

        IEnumerable<MissionModel> missions;

        if (request.ClusterId.HasValue)
            missions = await _missionRepository.GetByClusterIdAsync(request.ClusterId.Value, cancellationToken);
        else
            missions = await _missionRepository.GetAllAsync(cancellationToken);

        return new GetMissionsResponse
        {
            Missions = missions.Select(m => new MissionDto
            {
                Id = m.Id,
                ClusterId = m.ClusterId,
                MissionType = m.MissionType,
                PriorityScore = m.PriorityScore,
                Status = m.Status.ToString(),
                StartTime = m.StartTime,
                ExpectedEndTime = m.ExpectedEndTime,
                IsCompleted = m.IsCompleted,
                CreatedById = m.CreatedById,
                CreatedAt = m.CreatedAt,
                CompletedAt = m.CompletedAt,
                ActivityCount = m.Activities.Count,
                Activities = m.Activities.Select(a => new MissionActivityDto
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
                    Status = a.Status.ToString(),
                    AssignedAt = a.AssignedAt,
                    CompletedAt = a.CompletedAt
                }).ToList()
            }).ToList()
        };
    }
}
