using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

public class GetMissionsQueryHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionAiSuggestionRepository aiSuggestionRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    ILogger<GetMissionsQueryHandler> logger
) : IRequestHandler<GetMissionsQuery, GetMissionsResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IMissionAiSuggestionRepository _aiSuggestionRepository = aiSuggestionRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
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

        // Load teams per mission
        var teamsByMission = new Dictionary<int, List<MissionTeamModel>>();
        foreach (var m in missionList)
        {
            var teams = await _missionTeamRepository.GetByMissionIdAsync(m.Id, cancellationToken);
            teamsByMission[m.Id] = teams.ToList();
        }

        // Load AI suggestions grouped by cluster
        var clusterIds = missionList
            .Where(m => m.ClusterId.HasValue)
            .Select(m => m.ClusterId!.Value)
            .Distinct();

        var aiSuggestions = (await _aiSuggestionRepository.GetByClusterIdsAsync(clusterIds, cancellationToken))
            .Where(s => s.ClusterId.HasValue)
            .ToList();

        var aiSuggestionsByCluster = aiSuggestions
            .GroupBy(s => s.ClusterId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());
        var aiSuggestionsById = aiSuggestions.ToDictionary(s => s.Id);

        var response = new GetMissionsResponse
        {
            Missions = missionList.Select(m =>
            {
                var rawAi = m.AiSuggestionId.HasValue && aiSuggestionsById.TryGetValue(m.AiSuggestionId.Value, out var linkedAi)
                    ? linkedAi
                    : m.ClusterId.HasValue && aiSuggestionsByCluster.TryGetValue(m.ClusterId.Value, out var ai)
                        ? ai
                        : null;
                var aiModel = rawAi is not null ? MissionAiSuggestionSection.From(rawAi) : null;
                return new MissionDto
                {
                    Id = m.Id,
                    ClusterId = m.ClusterId,
                    MissionType = m.MissionType,
                    PriorityScore = m.PriorityScore,
                    Status = m.Status.ToString(),
                    StartTime = m.StartTime,
                    ExpectedEndTime = m.ExpectedEndTime,
                    CreatedAt = m.CreatedAt,
                    CompletedAt = m.CompletedAt,
                    ActivityCount = m.Activities.Count,
                    Teams = teamsByMission.TryGetValue(m.Id, out var missionTeams)
                        ? missionTeams.Select(t => new AssignedTeamDto
                        {
                            MissionTeamId = t.Id,
                            RescueTeamId = t.RescuerTeamId,
                            TeamName = t.TeamName,
                            TeamCode = t.TeamCode,
                            AssemblyPointId = t.AssemblyPointId,
                            AssemblyPointName = t.AssemblyPointName,
                            TeamType = t.TeamType,
                            Status = t.Status,
                            TeamStatus = t.TeamStatus,
                            MemberCount = t.MemberCount,
                            Latitude = t.Latitude,
                            Longitude = t.Longitude,
                            LocationUpdatedAt = t.LocationUpdatedAt,
                            AssignedAt = t.AssignedAt,
                            ReportStatus = t.ReportStatus,
                            ReportLastEditedAt = t.ReportLastEditedAt,
                            ReportSubmittedAt = t.ReportSubmittedAt,
                            Members = t.RescueTeamMembers.Select(m => new RescueTeamMemberDto
                            {
                                UserId = m.UserId,
                                FullName = m.FullName,
                                AvatarUrl = m.AvatarUrl,
                                RescuerType = m.RescuerType,
                                RoleInTeam = m.RoleInTeam,
                                IsLeader = m.IsLeader,
                                Status = m.Status,
                                CheckedIn = m.CheckedIn
                            }).ToList()
                        }).ToList()
                        : [],
                    Activities = m.Activities.Select(a => new MissionActivityDto
                    {
                        Id = a.Id,
                        Step = a.Step,
                        ActivityType = a.ActivityType,
                        Description = a.Description,
                        ImageUrl = a.ImageUrl,
                        Priority = a.Priority,
                        EstimatedTime = a.EstimatedTime,
                        SosRequestId = a.SosRequestId,
                        DepotId = a.DepotId,
                        DepotName = a.DepotName,
                        DepotAddress = a.DepotAddress,
                        AssemblyPointId = a.AssemblyPointId,
                        AssemblyPointName = a.AssemblyPointName,
                        AssemblyPointLatitude = a.AssemblyPointLatitude,
                        AssemblyPointLongitude = a.AssemblyPointLongitude,
                        SuppliesToCollect = MissionActivityDtoHelper.ParseSupplies(a.Items),
                        TargetLatitude = a.TargetLatitude,
                        TargetLongitude = a.TargetLongitude,
                        Status = a.Status.ToString(),
                        MissionTeamId = a.MissionTeamId,
                        AssignedAt = a.AssignedAt,
                        CompletedAt = a.CompletedAt,
                        CompletedBy = a.CompletedBy
                    }).ToList(),
                    AiSuggestionId = m.AiSuggestionId ?? aiModel?.Id,
                    SuggestedMissionTitle = aiModel?.SuggestedMissionTitle,
                    SuggestedMissionType = aiModel?.SuggestedMissionType,
                    SuggestedPriorityScore = aiModel?.SuggestedPriorityScore,
                    SuggestedSeverityLevel = aiModel?.SuggestedSeverityLevel,
                    AiSuggestion = aiModel,
                    ManualOverride = MissionManualOverrideJsonHelper.Parse(m.ManualOverrideMetadata)
                };
            }).ToList()
        };

        await MissionActivityDtoHelper.EnrichSupplyImageUrlsAsync(
            response.Missions.SelectMany(mission => mission.Activities),
            _itemModelMetadataRepository,
            cancellationToken);

        return response;
    }
}
