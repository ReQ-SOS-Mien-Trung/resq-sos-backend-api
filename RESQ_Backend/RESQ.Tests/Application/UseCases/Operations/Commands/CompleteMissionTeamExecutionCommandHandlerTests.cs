using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.CompleteMissionTeamExecution;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class CompleteMissionTeamExecutionCommandHandlerTests
{
    private static readonly Guid MemberId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionTeamDoesNotExist()
    {
        var handler = new CompleteMissionTeamExecutionCommandHandler(new StubMissionTeamRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CompleteMissionTeamExecutionCommand(1, 99, MemberId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenMissionIdDoesNotMatch()
    {
        var handler = new CompleteMissionTeamExecutionCommandHandler(
            new StubMissionTeamRepository(BuildMissionTeam(status: MissionTeamExecutionStatus.Assigned)));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CompleteMissionTeamExecutionCommand(999, 7, MemberId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenRequesterIsNotRescueTeamMember()
    {
        var handler = new CompleteMissionTeamExecutionCommandHandler(
            new StubMissionTeamRepository(BuildMissionTeam(status: MissionTeamExecutionStatus.Assigned)));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CompleteMissionTeamExecutionCommand(5, 7, StrangerId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenMissionTeamWasCancelled()
    {
        var handler = new CompleteMissionTeamExecutionCommandHandler(
            new StubMissionTeamRepository(BuildMissionTeam(status: MissionTeamExecutionStatus.Cancelled)));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CompleteMissionTeamExecutionCommand(5, 7, MemberId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenMissionTeamAlreadyReported()
    {
        var handler = new CompleteMissionTeamExecutionCommandHandler(
            new StubMissionTeamRepository(BuildMissionTeam(status: MissionTeamExecutionStatus.Reported)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CompleteMissionTeamExecutionCommand(5, 7, MemberId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesStatusToCompletedWaitingReport_WhenRequestIsValid()
    {
        var missionTeamRepository = new StubMissionTeamRepository(BuildMissionTeam(status: MissionTeamExecutionStatus.Assigned));
        var handler = new CompleteMissionTeamExecutionCommandHandler(missionTeamRepository);

        var response = await handler.Handle(
            new CompleteMissionTeamExecutionCommand(5, 7, MemberId, "Đã hoàn tất nhiệm vụ"),
            CancellationToken.None);

        Assert.Equal(5, response.MissionId);
        Assert.Equal(7, response.MissionTeamId);
        Assert.Equal(MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), response.Status);
        Assert.Equal("Đã hoàn tất nhiệm vụ", response.Note);
        Assert.Equal(7, missionTeamRepository.LastUpdatedId);
        Assert.Equal(MissionTeamExecutionStatus.CompletedWaitingReport.ToString(), missionTeamRepository.LastUpdatedStatus);
        Assert.Equal("Đã hoàn tất nhiệm vụ", missionTeamRepository.LastNote);
    }

    private static MissionTeamModel BuildMissionTeam(MissionTeamExecutionStatus status)
        => new()
        {
            Id = 7,
            MissionId = 5,
            Status = status.ToString(),
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo { UserId = MemberId }
            ]
        };

    private sealed class StubMissionTeamRepository(MissionTeamModel? missionTeam) : IMissionTeamRepository
    {
        public int? LastUpdatedId { get; private set; }
        public string? LastUpdatedStatus { get; private set; }
        public string? LastNote { get; private set; }

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(missionTeam);

        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default)
        {
            LastUpdatedId = id;
            LastUpdatedStatus = status;
            LastNote = note;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }
}
