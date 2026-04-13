using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class SyncMissionActivitiesCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReplaysStoredSnapshot_ByClientMutationId()
    {
        var existingResult = new MissionActivitySyncResultDto
        {
            ClientMutationId = Guid.NewGuid(),
            MissionId = 12,
            ActivityId = 99,
            TargetStatus = MissionActivityStatus.Succeed,
            BaseServerStatus = MissionActivityStatus.OnGoing,
            Outcome = MissionActivitySyncOutcomes.Duplicate,
            EffectiveStatus = MissionActivityStatus.Succeed,
            CurrentServerStatus = MissionActivityStatus.Succeed,
            ErrorCode = MissionActivitySyncErrorCodes.AlreadyAtTargetStatus,
            Message = "Replay"
        };

        var mutationRepository = new InMemoryMissionActivitySyncMutationRepository();
        mutationRepository.Seed(existingResult, Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 10, 0, 0, TimeSpan.Zero));

        var executionService = new StubMissionActivityStatusExecutionService();
        var handler = BuildHandler(new InMemoryMissionActivityRepository(), mutationRepository, executionService, new StubUnitOfWork());

        var response = await handler.Handle(
            new SyncMissionActivitiesCommand(Guid.NewGuid(),
            [
                CreateItem(existingResult.ClientMutationId, 12, 99, new DateTimeOffset(2026, 4, 10, 10, 0, 0, TimeSpan.Zero), MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed)
            ]),
            CancellationToken.None);

        Assert.Single(response.Results);
        Assert.Equal(MissionActivitySyncOutcomes.Duplicate, response.Results[0].Outcome);
        Assert.Equal("Replay", response.Results[0].Message);
        Assert.Empty(executionService.Calls);
        Assert.Equal(1, response.Summary.Duplicate);
    }

    [Fact]
    public async Task Handle_ReturnsMixedOutcomesAndSummary_InOriginalRequestOrder()
    {
        var activityRepository = new InMemoryMissionActivityRepository();
        activityRepository.Upsert(new MissionActivityModel { Id = 20, MissionId = 7, Status = MissionActivityStatus.Planned, ActivityType = "RESCUE" });
        activityRepository.Upsert(new MissionActivityModel { Id = 21, MissionId = 7, Status = MissionActivityStatus.Succeed, ActivityType = "RESCUE" });
        activityRepository.Upsert(new MissionActivityModel { Id = 22, MissionId = 7, Status = MissionActivityStatus.Failed, ActivityType = "RESCUE" });

        var executionService = new StubMissionActivityStatusExecutionService(activityRepository)
        {
            ResultFactory = item => new MissionActivityStatusExecutionResult
            {
                EffectiveStatus = item.requestedStatus,
                CurrentServerStatus = item.requestedStatus,
                ConsumedItems =
                [
                    new SupplyExecutionItemDto
                    {
                        ItemModelId = item.activityId,
                        ItemName = $"Item-{item.activityId}",
                        Quantity = 1
                    }
                ]
            }
        };

        var handler = BuildHandler(activityRepository, new InMemoryMissionActivitySyncMutationRepository(), executionService, new StubUnitOfWork());

        var response = await handler.Handle(
            new SyncMissionActivitiesCommand(Guid.NewGuid(),
            [
                CreateItem(Guid.NewGuid(), 7, 21, new DateTimeOffset(2026, 4, 10, 10, 2, 0, TimeSpan.Zero), MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed),
                CreateItem(Guid.NewGuid(), 7, 20, new DateTimeOffset(2026, 4, 10, 10, 0, 0, TimeSpan.Zero), MissionActivityStatus.Planned, MissionActivityStatus.OnGoing),
                CreateItem(Guid.NewGuid(), 7, 22, new DateTimeOffset(2026, 4, 10, 10, 3, 0, TimeSpan.Zero), MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed)
            ]),
            CancellationToken.None);

        Assert.Equal(3, response.Results.Count);
        Assert.Equal(MissionActivitySyncOutcomes.Duplicate, response.Results[0].Outcome);
        Assert.Equal(MissionActivitySyncOutcomes.Applied, response.Results[1].Outcome);
        Assert.Equal(MissionActivitySyncOutcomes.Conflict, response.Results[2].Outcome);
        Assert.Equal(1, response.Summary.Applied);
        Assert.Equal(1, response.Summary.Duplicate);
        Assert.Equal(1, response.Summary.Conflict);
        Assert.Equal(20, executionService.Calls.Single().activityId);
    }

    [Fact]
    public async Task Handle_ProcessesItemsInStableExecutionOrder()
    {
        var activityRepository = new InMemoryMissionActivityRepository();
        activityRepository.Upsert(new MissionActivityModel { Id = 30, MissionId = 8, Status = MissionActivityStatus.Planned, ActivityType = "RESCUE" });
        activityRepository.Upsert(new MissionActivityModel { Id = 31, MissionId = 8, Status = MissionActivityStatus.Planned, ActivityType = "RESCUE" });
        activityRepository.Upsert(new MissionActivityModel { Id = 32, MissionId = 9, Status = MissionActivityStatus.Planned, ActivityType = "RESCUE" });

        var executionService = new StubMissionActivityStatusExecutionService(activityRepository)
        {
            ResultFactory = item => new MissionActivityStatusExecutionResult
            {
                EffectiveStatus = item.requestedStatus,
                CurrentServerStatus = item.requestedStatus
            }
        };

        var handler = BuildHandler(activityRepository, new InMemoryMissionActivitySyncMutationRepository(), executionService, new StubUnitOfWork());

        await handler.Handle(
            new SyncMissionActivitiesCommand(Guid.NewGuid(),
            [
                CreateItem(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), 9, 32, new DateTimeOffset(2026, 4, 10, 11, 0, 0, TimeSpan.Zero), MissionActivityStatus.Planned, MissionActivityStatus.OnGoing),
                CreateItem(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), 8, 31, new DateTimeOffset(2026, 4, 10, 10, 0, 0, TimeSpan.Zero), MissionActivityStatus.Planned, MissionActivityStatus.OnGoing),
                CreateItem(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), 8, 30, new DateTimeOffset(2026, 4, 10, 10, 0, 0, TimeSpan.Zero), MissionActivityStatus.Planned, MissionActivityStatus.OnGoing)
            ]),
            CancellationToken.None);

        Assert.Equal(
            [30, 31, 32],
            executionService.Calls.Select(call => call.activityId).ToArray());
    }

    [Fact]
    public async Task Handle_MapsRejectedOutcome_WhenExecutionServiceThrowsTeamMismatch()
    {
        var activityRepository = new InMemoryMissionActivityRepository();
        activityRepository.Upsert(new MissionActivityModel { Id = 40, MissionId = 11, Status = MissionActivityStatus.OnGoing, ActivityType = "RESCUE" });

        var executionService = new StubMissionActivityStatusExecutionService(activityRepository)
        {
            ExceptionFactory = _ => MissionActivitySyncErrorCodes.WithCode(
                new ForbiddenException("Bạn không có quyền cập nhật trạng thái activity này. Activity được giao cho đội khác."),
                MissionActivitySyncErrorCodes.ForbiddenTeamMismatch)
        };
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(activityRepository, new InMemoryMissionActivitySyncMutationRepository(), executionService, unitOfWork);

        var response = await handler.Handle(
            new SyncMissionActivitiesCommand(Guid.NewGuid(),
            [
                CreateItem(Guid.NewGuid(), 11, 40, new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero), MissionActivityStatus.OnGoing, MissionActivityStatus.Succeed)
            ]),
            CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.Equal(MissionActivitySyncOutcomes.Rejected, result.Outcome);
        Assert.Equal(MissionActivitySyncErrorCodes.ForbiddenTeamMismatch, result.ErrorCode);
        Assert.Equal(1, unitOfWork.ClearTrackedChangesCalls);
    }

    private static SyncMissionActivitiesCommandHandler BuildHandler(
        InMemoryMissionActivityRepository activityRepository,
        InMemoryMissionActivitySyncMutationRepository mutationRepository,
        StubMissionActivityStatusExecutionService executionService,
        StubUnitOfWork unitOfWork)
        => new(
            activityRepository,
            mutationRepository,
            executionService,
            unitOfWork,
            NullLogger<SyncMissionActivitiesCommandHandler>.Instance);

    private static MissionActivitySyncItemDto CreateItem(
        Guid clientMutationId,
        int missionId,
        int activityId,
        DateTimeOffset queuedAt,
        MissionActivityStatus baseServerStatus,
        MissionActivityStatus targetStatus) => new()
        {
            ClientMutationId = clientMutationId,
            MissionId = missionId,
            ActivityId = activityId,
            QueuedAt = queuedAt,
            BaseServerStatus = baseServerStatus,
            TargetStatus = targetStatus
        };

    private sealed class InMemoryMissionActivityRepository : IMissionActivityRepository
    {
        private readonly Dictionary<int, MissionActivityModel> _activities = [];

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_activities.TryGetValue(id, out var activity) ? activity : null);

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_activities.Values.Where(activity => activity.MissionId == missionId).AsEnumerable());

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MissionActivityModel>());

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            _activities[activity.Id] = activity;
            return Task.FromResult(activity.Id);
        }

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            _activities[activity.Id] = activity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, CancellationToken cancellationToken = default)
        {
            _activities[activityId].Status = status;
            _activities[activityId].LastDecisionBy = decisionBy;
            return Task.CompletedTask;
        }

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Upsert(MissionActivityModel activity) => _activities[activity.Id] = activity;
    }

    private sealed class InMemoryMissionActivitySyncMutationRepository : IMissionActivitySyncMutationRepository
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, MissionActivitySyncMutationModel> _items = [];

        public Task<MissionActivitySyncMutationModel?> GetByClientMutationIdAsync(Guid clientMutationId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_items.TryGetValue(clientMutationId, out var item) ? Clone(item) : null);
            }
        }

        public Task<bool> TryBeginAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_items.ContainsKey(mutation.ClientMutationId))
                {
                    return Task.FromResult(false);
                }

                _items[mutation.ClientMutationId] = Clone(mutation);
                return Task.FromResult(true);
            }
        }

        public Task UpdateSnapshotAsync(MissionActivitySyncMutationModel mutation, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _items[mutation.ClientMutationId] = Clone(mutation);
                return Task.CompletedTask;
            }
        }

        public void Seed(MissionActivitySyncResultDto result, Guid userId, DateTimeOffset queuedAt)
        {
            _items[result.ClientMutationId] = new MissionActivitySyncMutationModel
            {
                ClientMutationId = result.ClientMutationId,
                UserId = userId,
                MissionId = result.MissionId,
                ActivityId = result.ActivityId,
                BaseServerStatus = result.BaseServerStatus,
                RequestedStatus = result.TargetStatus,
                QueuedAt = queuedAt,
                Outcome = result.Outcome,
                EffectiveStatus = result.EffectiveStatus,
                CurrentServerStatus = result.CurrentServerStatus,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
                ResponseSnapshotJson = MissionActivitySyncResultMapper.SerializeSnapshot(result),
                ProcessedAt = queuedAt
            };
        }

        private static MissionActivitySyncMutationModel Clone(MissionActivitySyncMutationModel item) => new()
        {
            Id = item.Id,
            ClientMutationId = item.ClientMutationId,
            UserId = item.UserId,
            MissionId = item.MissionId,
            ActivityId = item.ActivityId,
            BaseServerStatus = item.BaseServerStatus,
            RequestedStatus = item.RequestedStatus,
            QueuedAt = item.QueuedAt,
            Outcome = item.Outcome,
            EffectiveStatus = item.EffectiveStatus,
            CurrentServerStatus = item.CurrentServerStatus,
            ErrorCode = item.ErrorCode,
            Message = item.Message,
            ResponseSnapshotJson = item.ResponseSnapshotJson,
            ProcessedAt = item.ProcessedAt
        };
    }

    private sealed class StubMissionActivityStatusExecutionService(InMemoryMissionActivityRepository? activityRepository = null) : IMissionActivityStatusExecutionService
    {
        private readonly InMemoryMissionActivityRepository? _activityRepository = activityRepository;

        public List<(int expectedMissionId, int activityId, MissionActivityStatus requestedStatus, Guid decisionBy)> Calls { get; } = [];

        public Func<(int expectedMissionId, int activityId, MissionActivityStatus requestedStatus, Guid decisionBy), MissionActivityStatusExecutionResult>? ResultFactory { get; set; }

        public Func<(int expectedMissionId, int activityId, MissionActivityStatus requestedStatus, Guid decisionBy), Exception>? ExceptionFactory { get; set; }

        public Task<MissionActivityStatusExecutionResult> ApplyAsync(int expectedMissionId, int activityId, MissionActivityStatus requestedStatus, Guid decisionBy, CancellationToken cancellationToken = default)
        {
            var call = (expectedMissionId, activityId, requestedStatus, decisionBy);
            Calls.Add(call);

            var exception = ExceptionFactory?.Invoke(call);
            if (exception is not null)
            {
                throw exception;
            }

            var result = ResultFactory?.Invoke(call) ?? new MissionActivityStatusExecutionResult
            {
                EffectiveStatus = requestedStatus,
                CurrentServerStatus = requestedStatus
            };

            if (_activityRepository is not null)
            {
                _activityRepository.UpdateStatusAsync(activityId, result.EffectiveStatus, decisionBy, cancellationToken).GetAwaiter().GetResult();
            }

            return Task.FromResult(result);
        }
    }
}
