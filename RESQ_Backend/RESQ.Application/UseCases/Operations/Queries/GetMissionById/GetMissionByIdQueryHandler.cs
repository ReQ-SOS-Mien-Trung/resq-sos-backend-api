using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionById;

public class GetMissionByIdQueryHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionAiSuggestionRepository aiSuggestionRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    ILogger<GetMissionByIdQueryHandler> logger
) : IRequestHandler<GetMissionByIdQuery, MissionDto?>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IMissionAiSuggestionRepository _aiSuggestionRepository = aiSuggestionRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly ILogger<GetMissionByIdQueryHandler> _logger = logger;

    public async Task<MissionDto?> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null) return null;

        var missionTeams = await _missionTeamRepository.GetByMissionIdAsync(mission.Id, cancellationToken);

        MissionAiSuggestionSection? aiSection = null;
        if (mission.AiSuggestionId.HasValue)
        {
            var linked = await _aiSuggestionRepository.GetByIdAsync(mission.AiSuggestionId.Value, cancellationToken);
            if (linked is not null)
                aiSection = MissionAiSuggestionSection.From(linked);
        }

        if (aiSection is null && mission.ClusterId.HasValue)
        {
            var suggestions = await _aiSuggestionRepository.GetByClusterIdAsync(mission.ClusterId.Value, cancellationToken);
            var latest = suggestions.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
            if (latest is not null)
                aiSection = MissionAiSuggestionSection.From(latest);
        }

        var result = new MissionDto
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
            }).ToList(),
            Activities = mission.Activities.Select(a => new MissionActivityDto
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
            AiSuggestionId = mission.AiSuggestionId ?? aiSection?.Id,
            SuggestedMissionTitle = aiSection?.SuggestedMissionTitle,
            SuggestedMissionType = aiSection?.SuggestedMissionType,
            SuggestedPriorityScore = aiSection?.SuggestedPriorityScore,
            SuggestedSeverityLevel = aiSection?.SuggestedSeverityLevel,
            AiSuggestion = aiSection,
            ManualOverride = MissionManualOverrideJsonHelper.Parse(mission.ManualOverrideMetadata)
        };

        await MissionActivityDtoHelper.EnrichSupplyImageUrlsAsync(result.Activities, _itemModelMetadataRepository, cancellationToken);

        return result;
    }
}
