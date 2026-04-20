namespace RESQ.Application.UseCases.Operations.Shared;

public interface IRescueTeamMissionLifecycleSyncService
{
    Task<RescueTeamMissionLifecycleSyncResult> SyncTeamsToOnMissionAsync(
        IEnumerable<int> rescueTeamIds,
        CancellationToken cancellationToken = default);

    Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterReturnAsync(
        int rescueTeamId,
        CancellationToken cancellationToken = default);

    Task<RescueTeamMissionLifecycleSyncResult> SyncTeamToAvailableAfterExecutionAsync(
        int rescueTeamId,
        int missionTeamId,
        CancellationToken cancellationToken = default);

    Task PushRealtimeIfNeededAsync(
        RescueTeamMissionLifecycleSyncResult result,
        CancellationToken cancellationToken = default);
}

public sealed record RescueTeamMissionLifecycleSyncResult(IReadOnlyCollection<int> ChangedTeamIds)
{
    public static RescueTeamMissionLifecycleSyncResult None { get; } = new(Array.Empty<int>());

    public bool HasChanges => ChangedTeamIds.Count > 0;
}
