namespace RESQ.Application.Services;

public interface ISosRequestRealtimeHubService
{
    Task PushSosRequestUpdateAsync(
        int sosRequestId,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default);

    Task PushSosRequestUpdatesAsync(
        IEnumerable<int> sosRequestIds,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default);
}
