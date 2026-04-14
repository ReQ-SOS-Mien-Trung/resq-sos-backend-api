using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Mappers.Operations;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionActivitySyncMutationRepository(ResQDbContext dbContext) : IMissionActivitySyncMutationRepository
{
    private readonly ResQDbContext _dbContext = dbContext;

    public async Task<MissionActivitySyncMutationModel?> GetByClientMutationIdAsync(Guid clientMutationId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MissionActivitySyncMutations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientMutationId == clientMutationId, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<bool> TryBeginAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO mission_activity_sync_mutations
    (client_mutation_id, user_id, mission_id, activity_id, base_server_status, requested_status, queued_at, outcome, effective_status, current_server_status, error_code, message, response_snapshot_json, processed_at)
VALUES
    ({mutation.ClientMutationId}, {mutation.UserId}, {mutation.MissionId}, {mutation.ActivityId}, {MissionActivityMapper.ToDbString(mutation.BaseServerStatus)}, {MissionActivityMapper.ToDbString(mutation.RequestedStatus)}, {mutation.QueuedAt}, {mutation.Outcome}, {ToDbStatus(mutation.EffectiveStatus)}, {ToDbStatus(mutation.CurrentServerStatus)}, {mutation.ErrorCode}, {mutation.Message}, {"{}"}::jsonb, {mutation.ProcessedAt})
ON CONFLICT (client_mutation_id) DO NOTHING;", cancellationToken);

        return affected == 1;
    }

    public async Task UpdateSnapshotAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MissionActivitySyncMutations
            .SingleAsync(item => item.ClientMutationId == mutation.ClientMutationId, cancellationToken);

        entity.UserId = mutation.UserId;
        entity.MissionId = mutation.MissionId;
        entity.ActivityId = mutation.ActivityId;
        entity.BaseServerStatus = MissionActivityMapper.ToDbString(mutation.BaseServerStatus);
        entity.RequestedStatus = MissionActivityMapper.ToDbString(mutation.RequestedStatus);
        entity.QueuedAt = mutation.QueuedAt;
        entity.Outcome = mutation.Outcome;
        entity.EffectiveStatus = ToDbStatus(mutation.EffectiveStatus);
        entity.CurrentServerStatus = ToDbStatus(mutation.CurrentServerStatus);
        entity.ErrorCode = mutation.ErrorCode;
        entity.Message = mutation.Message;
        entity.ResponseSnapshotJson = mutation.ResponseSnapshotJson;
        entity.ProcessedAt = mutation.ProcessedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MissionActivitySyncMutationModel ToModel(MissionActivitySyncMutation entity) => new()
    {
        Id = entity.Id,
        ClientMutationId = entity.ClientMutationId,
        UserId = entity.UserId,
        MissionId = entity.MissionId,
        ActivityId = entity.ActivityId,
        BaseServerStatus = MissionActivityMapper.ToEnum(entity.BaseServerStatus),
        RequestedStatus = MissionActivityMapper.ToEnum(entity.RequestedStatus),
        QueuedAt = entity.QueuedAt,
        Outcome = entity.Outcome,
        EffectiveStatus = entity.EffectiveStatus is null ? null : MissionActivityMapper.ToEnum(entity.EffectiveStatus),
        CurrentServerStatus = entity.CurrentServerStatus is null ? null : MissionActivityMapper.ToEnum(entity.CurrentServerStatus),
        ErrorCode = entity.ErrorCode,
        Message = entity.Message,
        ResponseSnapshotJson = entity.ResponseSnapshotJson,
        ProcessedAt = entity.ProcessedAt
    };

    private static string? ToDbStatus(RESQ.Domain.Enum.Operations.MissionActivityStatus? status) =>
        status.HasValue ? MissionActivityMapper.ToDbString(status.Value) : null;
}
