using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = new UpdateMissionCommandHandler(
            new StubMissionRepository(null),
            new StubUnitOfWork(),
            NullLogger<UpdateMissionCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateMissionCommand(99, "Medical", 90.0, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesMissionFieldsAndSaves()
    {
        var mission = new MissionModel
        {
            Id = 15,
            MissionType = "Initial",
            PriorityScore = 10,
            Status = MissionStatus.Planned
        };
        var missionRepository = new StubMissionRepository(mission);
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateMissionCommandHandler(
            missionRepository,
            unitOfWork,
            NullLogger<UpdateMissionCommandHandler>.Instance);

        var startTime = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Unspecified);
        var expectedEndTime = new DateTime(2026, 4, 10, 12, 30, 0, DateTimeKind.Unspecified);

        var response = await handler.Handle(
            new UpdateMissionCommand(15, "Medical", 88.5, startTime, expectedEndTime),
            CancellationToken.None);

        Assert.True(missionRepository.UpdateWasCalled);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal("Medical", mission.MissionType);
        Assert.Equal(88.5, mission.PriorityScore);
        Assert.Equal(startTime, mission.StartTime);
        Assert.Equal(DateTimeKind.Utc, mission.StartTime!.Value.Kind);
        Assert.Equal(expectedEndTime, mission.ExpectedEndTime);
        Assert.Equal(DateTimeKind.Utc, mission.ExpectedEndTime!.Value.Kind);
        Assert.Equal(15, response.MissionId);
        Assert.Equal("Medical", response.MissionType);
        Assert.Equal(88.5, response.PriorityScore);
    }

    private sealed class StubMissionRepository(MissionModel? mission) : IMissionRepository
    {
        public bool UpdateWasCalled { get; private set; }

        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(mission);

        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default)
        {
            UpdateWasCalled = true;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}