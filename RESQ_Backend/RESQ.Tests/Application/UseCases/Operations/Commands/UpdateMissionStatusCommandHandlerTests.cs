using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionStatusCommandHandlerTests
{
    private static readonly Guid DecisionById = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = BuildHandler(
            new StubMissionRepository(null),
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(),
            new StubSosRequestRepository(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateMissionStatusCommand(99, MissionStatus.OnGoing, DecisionById), CancellationToken.None));
    }

    [Theory]
    [InlineData(MissionStatus.Completed)]
    [InlineData(MissionStatus.Incompleted)]
    public async Task Handle_ThrowsBadRequest_WhenRequestAttemptsToFinalizeMission(MissionStatus requestedStatus)
    {
        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 10, Status = MissionStatus.OnGoing }),
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(),
            new StubSosRequestRepository(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateMissionStatusCommand(10, requestedStatus, DecisionById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenStatusTransitionIsInvalid()
    {
        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 10, Status = MissionStatus.Completed }),
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(),
            new StubSosRequestRepository(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateMissionStatusCommand(10, MissionStatus.Planned, DecisionById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesMissionAndClusterSosStatuses_WhenMissionStarts()
    {
        var missionRepository = new StubMissionRepository(new MissionModel
        {
            Id = 15,
            ClusterId = 123,
            Status = MissionStatus.Planned
        });
        var sosRequestRepository = new StubSosRequestRepository();
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(
            missionRepository,
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(),
            sosRequestRepository,
            unitOfWork);

        var response = await handler.Handle(
            new UpdateMissionStatusCommand(15, MissionStatus.OnGoing, DecisionById),
            CancellationToken.None);

        Assert.True(missionRepository.UpdateStatusWasCalled);
        Assert.Equal(15, missionRepository.LastMissionId);
        Assert.Equal(MissionStatus.OnGoing, missionRepository.LastStatus);
        Assert.False(missionRepository.LastIsCompleted);
        Assert.Equal(123, sosRequestRepository.LastClusterId);
        Assert.Equal(SosRequestStatus.InProgress, sosRequestRepository.LastStatus);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(15, response.MissionId);
        Assert.Equal(MissionStatus.OnGoing.ToString(), response.Status);
        Assert.False(response.IsCompleted);
    }

    private static UpdateMissionStatusCommandHandler BuildHandler(
        IMissionRepository missionRepository,
        IMissionActivityRepository missionActivityRepository,
        IMissionTeamRepository missionTeamRepository,
        ISosRequestRepository sosRequestRepository,
        StubUnitOfWork unitOfWork)
        => new(
            missionRepository,
            missionActivityRepository,
            missionTeamRepository,
            sosRequestRepository,
            unitOfWork,
            NullLogger<UpdateMissionStatusCommandHandler>.Instance);

    private sealed class StubMissionRepository(MissionModel? mission) : IMissionRepository
    {
        public bool UpdateStatusWasCalled { get; private set; }
        public int LastMissionId { get; private set; }
        public MissionStatus LastStatus { get; private set; }
        public bool LastIsCompleted { get; private set; }

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(mission);

        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default)
        {
            UpdateStatusWasCalled = true;
            LastMissionId = missionId;
            LastStatus = status;
            LastIsCompleted = isCompleted;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMissionActivityRepository : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionActivityModel?>(null);
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMissionTeamRepository : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class StubSosRequestRepository : ISosRequestRepository
    {
        public int? LastClusterId { get; private set; }
        public SosRequestStatus? LastStatus { get; private set; }

        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
        {
            LastClusterId = clusterId;
            LastStatus = status;
            return Task.CompletedTask;
        }

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<SosRequestModel?>(null);
        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }
}
