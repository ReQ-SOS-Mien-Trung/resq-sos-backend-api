using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointUnavailableImpact;

public class GetAssemblyPointUnavailableImpactQueryHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IMissionActivityRepository missionActivityRepository,
    IRescueTeamRepository rescueTeamRepository)
    : IRequestHandler<GetAssemblyPointUnavailableImpactQuery, AssemblyPointUnavailableImpactResponse>
{
    public async Task<AssemblyPointUnavailableImpactResponse> Handle(
        GetAssemblyPointUnavailableImpactQuery request,
        CancellationToken cancellationToken)
    {
        var assemblyPoint = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        var alternatives = await assemblyPointRepository.GetAvailableAlternativesByDistanceAsync(
            request.AssemblyPointId,
            cancellationToken);

        var teams = await missionActivityRepository.GetReassignableAssemblyPointImpactsAsync(
            request.AssemblyPointId,
            cancellationToken);

        var teamlessRescuers = await assemblyPointRepository.GetTeamlessCheckedInRescuersAsync(
            request.AssemblyPointId,
            cancellationToken);

        var checkedInRescuers = await assemblyPointRepository.GetCheckedInRescuersAsync(
            request.AssemblyPointId,
            cancellationToken);

        var stationedTeams = await rescueTeamRepository.GetAvailableStationedTeamsByAssemblyPointAsync(
            request.AssemblyPointId,
            cancellationToken);

        return new AssemblyPointUnavailableImpactResponse
        {
            AssemblyPointId = assemblyPoint.Id,
            AssemblyPointCode = assemblyPoint.Code,
            AssemblyPointName = assemblyPoint.Name,
            CurrentStatus = assemblyPoint.Status.ToString(),
            StatusChangedAt = assemblyPoint.StatusChangedAt,
            AvailableAssemblyPoints = alternatives,
            RescueTeams = teams,
            StationedTeams = stationedTeams,
            TeamlessCheckedInRescuers = teamlessRescuers,
            CheckedInRescuers = checkedInRescuers
        };
    }
}
