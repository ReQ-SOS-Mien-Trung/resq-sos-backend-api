using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateTeamIncidentStatusCommandHandlerTests
{
    private static readonly Guid UpdatedById = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenIncidentDoesNotExist()
    {
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            new StubTeamIncidentRepository(null),
            new StubMissionTeamRepository(null),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateTeamIncidentStatusCommand(99, TeamIncidentStatus.InProgress, null, UpdatedById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenResolvingWithoutInjuryFlag()
    {
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            new StubTeamIncidentRepository(new TeamIncidentModel
            {
                Id = 5,
                MissionTeamId = 7,
                Status = TeamIncidentStatus.InProgress
            }),
            new StubMissionTeamRepository(null),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateTeamIncidentStatusCommand(5, TeamIncidentStatus.Resolved, null, UpdatedById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenTransitionIsInvalid()
    {
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            new StubTeamIncidentRepository(new TeamIncidentModel
            {
                Id = 5,
                MissionTeamId = 7,
                Status = TeamIncidentStatus.Reported
            }),
            new StubMissionTeamRepository(null),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateTeamIncidentStatusCommand(5, TeamIncidentStatus.Resolved, true, UpdatedById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesStatusAndSaves_WhenMovingToInProgress()
    {
        var teamIncidentRepository = new StubTeamIncidentRepository(new TeamIncidentModel
        {
            Id = 5,
            MissionTeamId = 7,
            Status = TeamIncidentStatus.Reported
        });
        var mediator = new RecordingMediator();
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            teamIncidentRepository,
            new StubMissionTeamRepository(null),
            mediator,
            unitOfWork);

        var response = await handler.Handle(
            new UpdateTeamIncidentStatusCommand(5, TeamIncidentStatus.InProgress, null, UpdatedById),
            CancellationToken.None);

        Assert.Equal(5, response.IncidentId);
        Assert.Equal(TeamIncidentStatus.InProgress.ToString(), response.Status);
        Assert.Equal(5, teamIncidentRepository.LastUpdatedId);
        Assert.Equal(TeamIncidentStatus.InProgress, teamIncidentRepository.LastUpdatedStatus);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Empty(mediator.SentRequests);
    }

    [Fact]
    public async Task Handle_ResolvesIncident_AndSendsResolveIncidentCommand_WhenMissionTeamExists()
    {
        var teamIncidentRepository = new StubTeamIncidentRepository(new TeamIncidentModel
        {
            Id = 5,
            MissionTeamId = 7,
            Status = TeamIncidentStatus.InProgress
        });
        var missionTeamRepository = new StubMissionTeamRepository(new MissionTeamModel
        {
            Id = 7,
            RescuerTeamId = 22
        });
        var mediator = new RecordingMediator();
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            teamIncidentRepository,
            missionTeamRepository,
            mediator,
            unitOfWork);

        var response = await handler.Handle(
            new UpdateTeamIncidentStatusCommand(5, TeamIncidentStatus.Resolved, true, UpdatedById),
            CancellationToken.None);

        Assert.Equal(TeamIncidentStatus.Resolved.ToString(), response.Status);
        Assert.Equal(1, unitOfWork.SaveCalls);
        var command = Assert.IsType<ResolveIncidentCommand>(Assert.Single(mediator.SentRequests));
        Assert.Equal(22, command.TeamId);
        Assert.True(command.HasInjuredMember);
    }

    [Fact]
    public async Task Handle_SwallowsMediatorErrors_WhenResolvingIncident()
    {
        var teamIncidentRepository = new StubTeamIncidentRepository(new TeamIncidentModel
        {
            Id = 5,
            MissionTeamId = 7,
            Status = TeamIncidentStatus.InProgress
        });
        var missionTeamRepository = new StubMissionTeamRepository(new MissionTeamModel
        {
            Id = 7,
            RescuerTeamId = 22
        });
        var mediator = new RecordingMediator(_ => new InvalidOperationException("boom"));
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateTeamIncidentStatusCommandHandler(
            teamIncidentRepository,
            missionTeamRepository,
            mediator,
            unitOfWork);

        var response = await handler.Handle(
            new UpdateTeamIncidentStatusCommand(5, TeamIncidentStatus.Resolved, false, UpdatedById),
            CancellationToken.None);

        Assert.Equal(TeamIncidentStatus.Resolved.ToString(), response.Status);
        Assert.Equal(5, teamIncidentRepository.LastUpdatedId);
        Assert.Equal(TeamIncidentStatus.Resolved, teamIncidentRepository.LastUpdatedStatus);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Single(mediator.SentRequests);
    }

    private sealed class StubTeamIncidentRepository(TeamIncidentModel? incident) : ITeamIncidentRepository
    {
        public int? LastUpdatedId { get; private set; }
        public TeamIncidentStatus? LastUpdatedStatus { get; private set; }

        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(incident);

        public Task UpdateStatusAsync(int id, TeamIncidentStatus status, CancellationToken cancellationToken = default)
        {
            LastUpdatedId = id;
            LastUpdatedStatus = status;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateSupportSosRequestIdAsync(int id, int? supportSosRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMissionTeamRepository(MissionTeamModel? missionTeam) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(missionTeam);

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }
}