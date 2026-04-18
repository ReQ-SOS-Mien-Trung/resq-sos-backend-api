using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Entities.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UnassignTeamFromMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionTeamDoesNotExist()
    {
        var handler = new UnassignTeamFromMissionCommandHandler(
            new StubMissionTeamRepository(null),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UnassignTeamFromMissionCommand(99, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeletesMissionTeam_SavesAndCancelsRescueTeamState()
    {
        var missionTeamRepository = new StubMissionTeamRepository(new MissionTeamModel
        {
            Id = 12,
            RescuerTeamId = 34
        });
        var mediator = new RecordingMediator();
        var unitOfWork = new StubUnitOfWork();

        var handler = new UnassignTeamFromMissionCommandHandler(
            missionTeamRepository,
            mediator,
            unitOfWork);

        var response = await handler.Handle(new UnassignTeamFromMissionCommand(12, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(12, response.MissionTeamId);
        Assert.True(missionTeamRepository.DeleteWasCalled);
        Assert.Equal(1, unitOfWork.SaveCalls);

        var sentCommand = Assert.IsType<ChangeTeamMissionStateCommand>(Assert.Single(mediator.SentRequests));
        Assert.Equal(34, sentCommand.TeamId);
        Assert.Equal("cancel", sentCommand.Action);
    }

    private sealed class StubMissionTeamRepository(MissionTeamModel? missionTeam) : IMissionTeamRepository
    {
        public bool DeleteWasCalled { get; private set; }

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(missionTeam);

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            DeleteWasCalled = true;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }
}
