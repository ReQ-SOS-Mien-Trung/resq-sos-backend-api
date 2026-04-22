using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Operations.Shared;

public class RescueTeamMissionLifecycleSyncService(
    IRescueTeamRepository rescueTeamRepository,
    IMissionTeamRepository missionTeamRepository,
    IOperationalHubService operationalHubService,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<RescueTeamMissionLifecycleSyncService> logger) : IRescueTeamMissionLifecycleSyncService
{
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
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
        CancellationToken cancellationToken = default) =>
        await SyncTeamToAvailableCoreAsync(
            rescueTeamId,
            completedMissionTeamId: null,
            source: "return-assembly-point",
            cancellationToken);

    public async Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterExecutionAsync(
        int rescueTeamId,
        int missionTeamId,
        CancellationToken cancellationToken = default) =>
        await SyncTeamToAvailableCoreAsync(
            rescueTeamId,
            completedMissionTeamId: missionTeamId,
            source: "completed-execution",
            cancellationToken);

    private async Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableCoreAsync(
        int rescueTeamId,
        int? completedMissionTeamId,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (rescueTeamId <= 0)
            return RescueTeamMissionLifecycleSyncResult.None;

        if (completedMissionTeamId.HasValue
            && await HasBlockingNewMissionAssignmentAsync(rescueTeamId, completedMissionTeamId.Value, source, cancellationToken))
        {
            return RescueTeamMissionLifecycleSyncResult.None;
        }

        RescueTeamModel? team;
        try
        {
            team = await _rescueTeamRepository.GetByIdAsync(rescueTeamId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to load RescueTeamId={RescueTeamId} for {Source} lifecycle sync.",
                rescueTeamId,
                source);
            return RescueTeamMissionLifecycleSyncResult.None;
        }

        if (team is null)
        {
            _logger.LogWarning(
                "Skipped {Source} lifecycle sync because RescueTeamId={RescueTeamId} was not found.",
                source,
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
                        "Skipped {Source} lifecycle sync for {TeamLabel} because status is {Status}.",
                        source,
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
                "Failed to sync {TeamLabel} back to Available after {Source}.",
                FormatTeamLabel(team),
                source);
            return RescueTeamMissionLifecycleSyncResult.None;
        }
    }

    private async Task<bool> HasBlockingNewMissionAssignmentAsync(
        int rescueTeamId,
        int completedMissionTeamId,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeAssignments = await _missionTeamRepository
                .GetActiveByRescuerTeamIdAsync(rescueTeamId, cancellationToken);

            var blockingAssignment = activeAssignments.FirstOrDefault(missionTeam =>
                missionTeam.Id != completedMissionTeamId
                && IsBlockingMissionTeamStatus(missionTeam.Status));

            if (blockingAssignment is null)
            {
                return false;
            }

            _logger.LogInformation(
                "Skipped {Source} rescue-team availability sync for RescueTeamId={RescueTeamId} because MissionTeamId={MissionTeamId} is already {Status}.",
                source,
                rescueTeamId,
                blockingAssignment.Id,
                blockingAssignment.Status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Skipped {Source} rescue-team availability sync for RescueTeamId={RescueTeamId} because active mission assignments could not be verified.",
                source,
                rescueTeamId);
            return true;
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
            foreach (var teamId in result.ChangedTeamIds)
            {
                await _adminRealtimeHubService.PushRescueTeamUpdateAsync(
                    new RESQ.Application.Common.Models.AdminRescueTeamRealtimeUpdate
                    {
                        EntityId = teamId,
                        EntityType = "RescueTeam",
                        TeamId = teamId,
                        Action = "StatusChanged",
                        Status = null,
                        ChangedAt = DateTime.UtcNow
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to push rescue-team lifecycle realtime update for TeamIds=[{TeamIds}]",
                string.Join(", ", result.ChangedTeamIds));
        }
    }

    private static bool IsBlockingMissionTeamStatus(string? status) =>
        string.Equals(status, MissionTeamExecutionStatus.Assigned.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, MissionTeamExecutionStatus.InProgress.ToString(), StringComparison.OrdinalIgnoreCase);

    private static string FormatTeamLabel(RescueTeamModel team) =>
        string.IsNullOrWhiteSpace(team.Name)
            ? $"Đội #{team.Id}"
            : $"Đội #{team.Id} ({team.Name})";
}
