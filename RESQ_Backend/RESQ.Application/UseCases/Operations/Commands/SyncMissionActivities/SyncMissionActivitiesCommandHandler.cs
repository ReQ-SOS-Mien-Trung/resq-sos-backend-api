using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;

public class SyncMissionActivitiesCommandHandler(
    IMissionActivityRepository activityRepository,
    IMissionActivitySyncMutationRepository syncMutationRepository,
    IMissionActivityStatusExecutionService missionActivityStatusExecutionService,
    IUnitOfWork unitOfWork,
    ILogger<SyncMissionActivitiesCommandHandler> logger
) : IRequestHandler<SyncMissionActivitiesCommand, SyncMissionActivitiesResponseDto>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionActivitySyncMutationRepository _syncMutationRepository = syncMutationRepository;
    private readonly IMissionActivityStatusExecutionService _missionActivityStatusExecutionService = missionActivityStatusExecutionService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<SyncMissionActivitiesCommandHandler> _logger = logger;

    public async Task<SyncMissionActivitiesResponseDto> Handle(SyncMissionActivitiesCommand request, CancellationToken cancellationToken)
    {
        var orderedItems = request.Items
            .Select((item, index) => new IndexedSyncItem(index, item))
            .OrderBy(item => item.Item.QueuedAt)
            .ThenBy(item => item.Item.MissionId)
            .ThenBy(item => item.Item.ActivityId)
            .ThenBy(item => item.Item.ClientMutationId)
            .ToList();

        var resultsByIndex = new MissionActivitySyncResultDto[request.Items.Count];

        foreach (var indexedItem in orderedItems)
        {
            resultsByIndex[indexedItem.OriginalIndex] = await ProcessItemAsync(
                request.UserId,
                indexedItem.Item,
                cancellationToken);
        }

        var results = resultsByIndex.ToList();
        return new SyncMissionActivitiesResponseDto
        {
            Summary = MissionActivitySyncResultMapper.BuildSummary(results),
            Results = results
        };
    }

    private async Task<MissionActivitySyncResultDto> ProcessItemAsync(
        Guid userId,
        MissionActivitySyncItemDto item,
        CancellationToken cancellationToken)
    {
        var replay = await TryReplayAsync(item.ClientMutationId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        MissionActivitySyncResultDto? result = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                result = await ProcessItemInTransactionAsync(userId, item, cancellationToken);
            });

            return result!;
        }
        catch (DeferredMissionActivitySyncResultException ex)
        {
            _unitOfWork.ClearTrackedChanges();
            return await PersistDeferredResultAsync(userId, item, ex.Result, cancellationToken);
        }
    }

    private async Task<MissionActivitySyncResultDto> ProcessItemInTransactionAsync(
        Guid userId,
        MissionActivitySyncItemDto item,
        CancellationToken cancellationToken)
    {
        var replay = await TryReplayAsync(item.ClientMutationId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var placeholder = CreatePlaceholderMutation(userId, item);
        var began = await _syncMutationRepository.TryBeginAsync(placeholder, cancellationToken);
        if (!began)
        {
            return await GetRequiredReplayAsync(item.ClientMutationId, cancellationToken);
        }

        var activity = await _activityRepository.GetByIdAsync(item.ActivityId, cancellationToken);
        if (activity is null)
        {
            var notFoundResult = MissionActivitySyncResultMapper.CreateRejected(
                item,
                MissionActivitySyncErrorCodes.ActivityNotFound,
                $"Không tìm thấy activity với ID: {item.ActivityId}",
                null);

            await PersistSnapshotAsync(userId, item, notFoundResult, cancellationToken);
            return notFoundResult;
        }

        if (activity.MissionId != item.MissionId)
        {
            var mismatchResult = MissionActivitySyncResultMapper.CreateRejected(
                item,
                MissionActivitySyncErrorCodes.MissionActivityMismatch,
                "Activity này không thuộc mission được chỉ định.",
                activity.Status);

            await PersistSnapshotAsync(userId, item, mismatchResult, cancellationToken);
            return mismatchResult;
        }

        if (activity.Status == item.BaseServerStatus)
        {
            try
            {
                var executionResult = await _missionActivityStatusExecutionService.ApplyAsync(
                    item.MissionId,
                    item.ActivityId,
                    item.TargetStatus,
                    userId,
                    cancellationToken);

                var appliedResult = MissionActivitySyncResultMapper.CreateApplied(item, executionResult);
                await PersistSnapshotAsync(userId, item, appliedResult, cancellationToken);
                return appliedResult;
            }
            catch (NotFoundException ex)
            {
                throw new DeferredMissionActivitySyncResultException(
                    MissionActivitySyncResultMapper.CreateRejected(item, MissionActivitySyncErrorCodes.ActivityNotFound, ex.Message, null),
                    ex);
            }
            catch (ForbiddenException ex)
            {
                throw new DeferredMissionActivitySyncResultException(
                    MissionActivitySyncResultMapper.CreateRejected(item, MissionActivitySyncErrorCodes.ForbiddenTeamMismatch, ex.Message, activity.Status),
                    ex);
            }
            catch (BadRequestException ex)
            {
                var errorCode = MissionActivitySyncErrorCodes.TryGet(ex) ?? MissionActivitySyncErrorCodes.InvalidStatusTransition;
                throw new DeferredMissionActivitySyncResultException(
                    MissionActivitySyncResultMapper.CreateRejected(item, errorCode, ex.Message, activity.Status),
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Mission activity sync failed unexpectedly for ClientMutationId={ClientMutationId} ActivityId={ActivityId}",
                    item.ClientMutationId,
                    item.ActivityId);

                throw new DeferredMissionActivitySyncResultException(
                    MissionActivitySyncResultMapper.CreateFailed(item, "Đã xảy ra lỗi máy chủ khi đồng bộ activity.", activity.Status),
                    ex);
            }
        }

        var result = activity.Status == item.TargetStatus
            ? MissionActivitySyncResultMapper.CreateDuplicate(item)
            : MissionActivitySyncResultMapper.CreateConflict(item, activity.Status);

        await PersistSnapshotAsync(userId, item, result, cancellationToken);
        return result;
    }

    private async Task<MissionActivitySyncResultDto> PersistDeferredResultAsync(
        Guid userId,
        MissionActivitySyncItemDto item,
        MissionActivitySyncResultDto result,
        CancellationToken cancellationToken)
    {
        var replay = await TryReplayAsync(item.ClientMutationId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        MissionActivitySyncResultDto? persistedResult = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            persistedResult = await TryReplayAsync(item.ClientMutationId, cancellationToken);
            if (persistedResult is not null)
            {
                return;
            }

            var placeholder = CreatePlaceholderMutation(userId, item);
            var began = await _syncMutationRepository.TryBeginAsync(placeholder, cancellationToken);
            if (!began)
            {
                persistedResult = await TryReplayAsync(item.ClientMutationId, cancellationToken);
                if (persistedResult is not null)
                {
                    return;
                }

                await PersistSnapshotAsync(userId, item, result, cancellationToken);
                persistedResult = result;
                return;
            }

            await PersistSnapshotAsync(userId, item, result, cancellationToken);
            persistedResult = result;
        });

        return persistedResult!;
    }

    private async Task<MissionActivitySyncResultDto?> TryReplayAsync(Guid clientMutationId, CancellationToken cancellationToken)
    {
        var existing = await _syncMutationRepository.GetByClientMutationIdAsync(clientMutationId, cancellationToken);
        if (existing is null || existing.Outcome == MissionActivitySyncOutcomes.Processing)
        {
            return null;
        }

        return MissionActivitySyncResultMapper.DeserializeSnapshot(existing.ResponseSnapshotJson);
    }

    private async Task<MissionActivitySyncResultDto> GetRequiredReplayAsync(Guid clientMutationId, CancellationToken cancellationToken) =>
        await TryReplayAsync(clientMutationId, cancellationToken)
        ?? throw new InvalidOperationException($"Không tìm thấy response snapshot cho ClientMutationId={clientMutationId}.");

    private MissionActivitySyncMutationModel CreatePlaceholderMutation(Guid userId, MissionActivitySyncItemDto item) => new()
    {
        ClientMutationId = item.ClientMutationId,
        UserId = userId,
        MissionId = item.MissionId,
        ActivityId = item.ActivityId,
        BaseServerStatus = item.BaseServerStatus,
        RequestedStatus = item.TargetStatus,
        QueuedAt = item.QueuedAt,
        Outcome = MissionActivitySyncOutcomes.Processing,
        ResponseSnapshotJson = "{}",
        ProcessedAt = DateTimeOffset.UtcNow
    };

    private async Task PersistSnapshotAsync(
        Guid userId,
        MissionActivitySyncItemDto item,
        MissionActivitySyncResultDto result,
        CancellationToken cancellationToken)
    {
        var mutation = new MissionActivitySyncMutationModel
        {
            ClientMutationId = item.ClientMutationId,
            UserId = userId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            BaseServerStatus = item.BaseServerStatus,
            RequestedStatus = item.TargetStatus,
            QueuedAt = item.QueuedAt,
            Outcome = result.Outcome,
            EffectiveStatus = result.EffectiveStatus,
            CurrentServerStatus = result.CurrentServerStatus,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            ResponseSnapshotJson = MissionActivitySyncResultMapper.SerializeSnapshot(result),
            ProcessedAt = DateTimeOffset.UtcNow
        };

        await _syncMutationRepository.UpdateSnapshotAsync(mutation, cancellationToken);
    }

    private sealed record IndexedSyncItem(int OriginalIndex, MissionActivitySyncItemDto Item);

    private sealed class DeferredMissionActivitySyncResultException(
        MissionActivitySyncResultDto result,
        Exception innerException) : Exception("Mission activity sync item failed and must be persisted after rollback.", innerException)
    {
        public MissionActivitySyncResultDto Result { get; } = result;
    }
}