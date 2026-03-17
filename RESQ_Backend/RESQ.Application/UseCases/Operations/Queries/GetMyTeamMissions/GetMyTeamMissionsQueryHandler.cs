using MediatR;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMyTeamMissions;

public class GetMyTeamMissionsQueryHandler(
    IPersonnelQueryRepository personnelQueryRepository,
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository)
    : IRequestHandler<GetMyTeamMissionsQuery, GetMissionsResponse>
{
    private readonly IPersonnelQueryRepository _personnelQueryRepository = personnelQueryRepository;
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;

    public async Task<GetMissionsResponse> Handle(GetMyTeamMissionsQuery request, CancellationToken cancellationToken)
    {
        var team = await _personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(request.UserId, cancellationToken);
        if (team is null)
            return new GetMissionsResponse { Missions = [] };

        var assignments = (await _missionTeamRepository.GetActiveByRescuerTeamIdAsync(team.Id, cancellationToken)).ToList();
        var missionIds = assignments
            .Select(a => a.MissionId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (missionIds.Count == 0)
            return new GetMissionsResponse { Missions = [] };

        var missions = (await _missionRepository.GetByIdsAsync(missionIds, cancellationToken)).ToList();

        var assignedByMission = assignments
            .GroupBy(a => a.MissionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new GetMissionsResponse
        {
            Missions = missions.Select(m => ToMissionDto(m, assignedByMission.TryGetValue(m.Id, out var teams) ? teams : [])).ToList()
        };
    }

    private static MissionDto ToMissionDto(MissionModel m, List<MissionTeamModel> assignedTeams)
    {
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
            Teams = assignedTeams.Select(t => new AssignedTeamDto
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
            Activities = m.Activities.Select(a => new MissionActivityDto
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
            }).ToList()
        };
    }
}
