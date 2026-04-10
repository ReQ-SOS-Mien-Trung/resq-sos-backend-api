using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionActivitySyncMutationRepository
{
    Task<MissionActivitySyncMutationModel?> GetByClientMutationIdAsync(Guid clientMutationId, CancellationToken cancellationToken = default);
    Task<bool> TryBeginAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default);
    Task UpdateSnapshotAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default);
}