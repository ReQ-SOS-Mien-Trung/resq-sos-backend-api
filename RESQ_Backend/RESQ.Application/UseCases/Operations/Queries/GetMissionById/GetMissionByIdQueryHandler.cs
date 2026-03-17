using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionById;

public class GetMissionByIdQueryHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionAiSuggestionRepository aiSuggestionRepository,
    ILogger<GetMissionByIdQueryHandler> logger
) : IRequestHandler<GetMissionByIdQuery, MissionDto?>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IMissionAiSuggestionRepository _aiSuggestionRepository = aiSuggestionRepository;
    private readonly ILogger<GetMissionByIdQueryHandler> _logger = logger;

    public async Task<MissionDto?> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null) return null;

        var missionTeams = await _missionTeamRepository.GetByMissionIdAsync(mission.Id, cancellationToken);

        MissionAiSuggestionSection? aiSection = null;
        if (mission.ClusterId.HasValue)
        {
            var suggestions = await _aiSuggestionRepository.GetByClusterIdAsync(mission.ClusterId.Value, cancellationToken);
            var latest = suggestions.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
            if (latest is not null)
                aiSection = MissionAiSuggestionSection.From(latest);
        }

        return new MissionDto
        {
            Id = mission.Id,
            ClusterId = mission.ClusterId,
            MissionType = mission.MissionType,
            PriorityScore = mission.PriorityScore,
            Status = mission.Status.ToString(),
            StartTime = mission.StartTime,
            ExpectedEndTime = mission.ExpectedEndTime,
            CreatedAt = mission.CreatedAt,
            CompletedAt = mission.CompletedAt,
            ActivityCount = mission.Activities.Count,
            Teams = missionTeams.Select(t => new AssignedTeamDto
            {
                MissionTeamId = t.Id,
                RescueTeamId = t.RescuerTeamId,
                TeamName = t.TeamName,
                TeamCode = t.TeamCode,
                AssemblyPointName = t.AssemblyPointName,
                TeamType = t.TeamType,
                Status = t.Status,
                TeamStatus = t.TeamStatus,
                MemberCount = t.MemberCount,
                Latitude = t.Latitude,
                Longitude = t.Longitude,
                LocationUpdatedAt = t.LocationUpdatedAt,
                AssignedAt = t.AssignedAt,
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
            }).ToList(),
            Activities = mission.Activities.Select(a => new MissionActivityDto
            {
                Id = a.Id,
                Step = a.Step,
                ActivityCode = a.ActivityCode,
                ActivityType = a.ActivityType,
                Description = a.Description,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                SuppliesToCollect = MissionActivityDtoHelper.ParseSupplies(a.Items),
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = a.Status.ToString(),
                MissionTeamId = a.MissionTeamId,
                AssignedAt = a.AssignedAt,
                CompletedAt = a.CompletedAt
            }).ToList(),
            AiSuggestionId = aiSection?.Id,
            SuggestedMissionTitle = aiSection?.SuggestedMissionTitle,
            SuggestedMissionType = aiSection?.SuggestedMissionType,
            SuggestedPriorityScore = aiSection?.SuggestedPriorityScore,
            SuggestedSeverityLevel = aiSection?.SuggestedSeverityLevel
        };
    }
}
