using MediatR;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeams;

public class GetMissionTeamsQueryHandler(
    IMissionTeamRepository missionTeamRepository
) : IRequestHandler<GetMissionTeamsQuery, GetMissionTeamsResponse>
{
    public async Task<GetMissionTeamsResponse> Handle(GetMissionTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await missionTeamRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        return new GetMissionTeamsResponse
        {
            MissionId = request.MissionId,
            Teams = teams.Select(t => new MissionTeamDto
            {
                MissionTeamId = t.Id,
                RescueTeamId = t.RescuerTeamId,
                TeamName = t.TeamName,
                TeamCode = t.TeamCode,
                AssemblyPointId = t.AssemblyPointId,
                AssemblyPointName = t.AssemblyPointName,
                TeamType = t.TeamType,
                Status = t.Status,
                Note = t.Note,
                Latitude = t.Latitude,
                Longitude = t.Longitude,
                LocationUpdatedAt = t.LocationUpdatedAt,
                LocationSource = t.LocationSource,
                AssignedAt = t.AssignedAt,
                UnassignedAt = t.UnassignedAt,
                SafetyLatestCheckInAt = t.SafetyLatestCheckInAt,
                SafetyTimeoutAt = t.SafetyTimeoutAt,
                SafetyStatus = t.SafetyStatus,
                GeneratedSosRequestId = t.GeneratedSosRequestId,
                ReportStatus = t.ReportStatus,
                ReportStartedAt = t.ReportStartedAt,
                ReportLastEditedAt = t.ReportLastEditedAt,
                ReportSubmittedAt = t.ReportSubmittedAt
            }).ToList()
        };
    }
}
