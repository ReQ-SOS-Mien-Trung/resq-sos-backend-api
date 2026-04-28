using RESQ.Application.Services;

namespace RESQ.Tests.TestDoubles;

/// <summary>No-op stub for ISosRequestRealtimeHubService used in unit tests.</summary>
internal sealed class StubSosRequestRealtimeHubService : ISosRequestRealtimeHubService
{
    public Task PushSosRequestUpdateAsync(
        int sosRequestId,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushSosRequestUpdatesAsync(
        IEnumerable<int> sosRequestIds,
        string action,
        int? previousClusterId = null,
        bool notifyUnclustered = false,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
