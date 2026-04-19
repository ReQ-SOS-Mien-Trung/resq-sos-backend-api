using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Operations.Shared;

public class RescueTeamMissionLifecycleSyncService(
    IRescueTeamRepository rescueTeamRepository,
    IOperationalHubService operationalHubService,
    ILogger<RescueTeamMissionLifecycleSyncService> logger) : IRescueTeamMissionLifecycleSyncService
{
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly ILogger<RescueTeamMissionLifecycleSyncService> _logger = logger;

    public async Task<RescueTeamMissionLifecycleSyncResult> SyncTeamsToOnMissionAsync(
        IEnumerable<int> rescueTeamIds,
        CancellationToken cancellationToken = default)
    {
        var distinctTeamIds = rescueTeamIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (distinctTeamIds.Count == 0)
            return RescueTeamMissionLifecycleSyncResult.None;

        var teams = new List<RescueTeamModel>(distinctTeamIds.Count);
        foreach (var teamId in distinctTeamIds)
        {
            var team = await _rescueTeamRepository.GetByIdAsync(teamId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với ID: {teamId}");

            teams.Add(team);
        }

        var invalidTeams = teams
            .Where(team => team.Status is not RescueTeamStatus.Assigned and not RescueTeamStatus.OnMission)
            .Select(team => $"{FormatTeamLabel(team)} đang ở trạng thái '{team.Status}'.")
            .ToList();

        if (invalidTeams.Count > 0)
        {
            throw new ConflictException(
                "Không thể bắt đầu mission vì một số đội cứu hộ chưa ở trạng thái Assigned:\n"
                + string.Join("\n", invalidTeams));
        }

        var changedTeamIds = new List<int>();
        foreach (var team in teams.Where(team => team.Status == RescueTeamStatus.Assigned))
        {
            team.StartMission();
            await _rescueTeamRepository.UpdateAsync(team, cancellationToken);
            changedTeamIds.Add(team.Id);
        }

        return changedTeamIds.Count == 0
            ? RescueTeamMissionLifecycleSyncResult.None
            : new RescueTeamMissionLifecycleSyncResult(changedTeamIds);
    }

    public async Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterReturnAsync(
        int rescueTeamId,
        CancellationToken cancellationToken = default)
    {
        if (rescueTeamId <= 0)
            return RescueTeamMissionLifecycleSyncResult.None;

        RescueTeamModel? team;
        try
        {
            team = await _rescueTeamRepository.GetByIdAsync(rescueTeamId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to load RescueTeamId={RescueTeamId} for return-assembly-point lifecycle sync.",
                rescueTeamId);
            return RescueTeamMissionLifecycleSyncResult.None;
        }

        if (team is null)
        {
            _logger.LogWarning(
                "Skipped return-assembly-point lifecycle sync because RescueTeamId={RescueTeamId} was not found.",
                rescueTeamId);
            return RescueTeamMissionLifecycleSyncResult.None;
        }

        try
        {
            switch (team.Status)
            {
                case RescueTeamStatus.OnMission:
                    team.FinishMission();
                    break;
                case RescueTeamStatus.Assigned:
                    team.CancelMission();
                    break;
                case RescueTeamStatus.Available:
                    return RescueTeamMissionLifecycleSyncResult.None;
                default:
                    _logger.LogWarning(
                        "Skipped return-assembly-point lifecycle sync for {TeamLabel} because status is {Status}.",
                        FormatTeamLabel(team),
                        team.Status);
                    return RescueTeamMissionLifecycleSyncResult.None;
            }

            await _rescueTeamRepository.UpdateAsync(team, cancellationToken);
            return new RescueTeamMissionLifecycleSyncResult([team.Id]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to sync {TeamLabel} back to Available after RETURN_ASSEMBLY_POINT.",
                FormatTeamLabel(team));
            return RescueTeamMissionLifecycleSyncResult.None;
        }
    }

    public async Task PushRealtimeIfNeededAsync(
        RescueTeamMissionLifecycleSyncResult result,
        CancellationToken cancellationToken = default)
    {
        if (!result.HasChanges)
            return;

        try
        {
            await _operationalHubService.PushLogisticsUpdateAsync("rescue-teams", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to push rescue-team lifecycle realtime update for TeamIds=[{TeamIds}]",
                string.Join(", ", result.ChangedTeamIds));
        }
    }

    private static string FormatTeamLabel(RescueTeamModel team) =>
        string.IsNullOrWhiteSpace(team.Name)
            ? $"Đội #{team.Id}"
            : $"Đội #{team.Id} ({team.Name})";
}
