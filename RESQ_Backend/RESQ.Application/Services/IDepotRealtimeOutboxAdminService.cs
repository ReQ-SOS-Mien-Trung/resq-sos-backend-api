namespace RESQ.Application.Services;

public interface IDepotRealtimeOutboxAdminService
{
    Task<int> ReplayDeadLettersAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default);
}
