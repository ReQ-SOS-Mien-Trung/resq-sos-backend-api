namespace RESQ.Application.Repositories.Emergency;

public record SosRequestCompanionRecord(int Id, int SosRequestId, Guid UserId, string? PhoneNumber, DateTime AddedAt);

public interface ISosRequestCompanionRepository
{
    Task CreateRangeAsync(IEnumerable<SosRequestCompanionRecord> companions, CancellationToken cancellationToken = default);
    Task<List<SosRequestCompanionRecord>> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default);
    Task<List<int>> GetSosRequestIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsCompanionAsync(int sosRequestId, Guid userId, CancellationToken cancellationToken = default);
}
