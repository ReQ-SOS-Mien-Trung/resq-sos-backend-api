using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public class GetMissionsQueryHandler(
    IMissionRepository missionRepository,
    IMissionAiSuggestionRepository aiSuggestionRepository,
    ILogger<GetMissionsQueryHandler> logger
) : IRequestHandler<GetMissionsQuery, GetMissionsResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionAiSuggestionRepository _aiSuggestionRepository = aiSuggestionRepository;
    private readonly ILogger<GetMissionsQueryHandler> _logger = logger;

    public async Task<GetMissionsResponse> Handle(GetMissionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting missions, ClusterId={clusterId}", request.ClusterId);

        IEnumerable<MissionModel> missions;

        if (request.ClusterId.HasValue)
            missions = await _missionRepository.GetByClusterIdAsync(request.ClusterId.Value, cancellationToken);
        else
            missions = await _missionRepository.GetAllAsync(cancellationToken);

        var missionList = missions.ToList();

        // Load AI suggestions grouped by cluster
        var clusterIds = missionList
            .Where(m => m.ClusterId.HasValue)
            .Select(m => m.ClusterId!.Value)
            .Distinct();

        var aiSuggestions = (await _aiSuggestionRepository.GetByClusterIdsAsync(clusterIds, cancellationToken))
            .Where(s => s.ClusterId.HasValue)
            .GroupBy(s => s.ClusterId!.Value)
            .ToDictionary(g => (int?)g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

        return new GetMissionsResponse
        {
            Missions = missionList.Select(m =>
            {
                aiSuggestions.TryGetValue(m.ClusterId, out var aiModel);
                return new MissionDto
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
                        SuppliesToCollect = MissionActivityDtoHelper.ParseSupplies(a.Items),
                        TargetLatitude = a.TargetLatitude,
                        TargetLongitude = a.TargetLongitude,
                        Status = a.Status.ToString(),
                        AssignedAt = a.AssignedAt,
                        CompletedAt = a.CompletedAt,
                        LastDecisionBy = a.LastDecisionBy
                    }).ToList(),
                    //AiSuggestion = aiModel is not null ? MissionAiSuggestionSection.From(aiModel) : null
                };
            }).ToList()
        };
    }
}
