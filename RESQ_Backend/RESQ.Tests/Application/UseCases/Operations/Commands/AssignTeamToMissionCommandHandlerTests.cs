using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class AssignTeamToMissionCommandHandlerTests
{
    private static readonly Guid AssignedById = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = new AssignTeamToMissionCommandHandler(
            new StubMissionRepository(null),
            new StubMissionTeamRepository(),
            new StubRescueTeamRepository(BuildTeam(2)),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AssignTeamToMissionCommand(10, 2, AssignedById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenRescueTeamDoesNotExist()
    {
        var handler = new AssignTeamToMissionCommandHandler(
            new StubMissionRepository(new MissionModel { Id = 10 }),
            new StubMissionTeamRepository(),
            new StubRescueTeamRepository(null),
            new RecordingMediator(),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AssignTeamToMissionCommand(10, 2, AssignedById), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CreatesMissionTeam_SavesAndUpdatesRescueTeamState()
    {
        var missionTeamRepository = new StubMissionTeamRepository(createdId: 55);
        var mediator = new RecordingMediator();
        var unitOfWork = new StubUnitOfWork();

        var handler = new AssignTeamToMissionCommandHandler(
            new StubMissionRepository(new MissionModel { Id = 10 }),
            missionTeamRepository,
            new StubRescueTeamRepository(BuildTeam(2)),
            mediator,
            unitOfWork);

        var response = await handler.Handle(new AssignTeamToMissionCommand(10, 2, AssignedById), CancellationToken.None);

        Assert.Equal(55, response.MissionTeamId);
        Assert.Equal(10, response.MissionId);
        Assert.Equal(2, response.RescueTeamId);
        Assert.Equal(MissionTeamExecutionStatus.Assigned.ToString(), response.Status);
        Assert.Equal(1, unitOfWork.SaveCalls);

        var created = Assert.IsType<MissionTeamModel>(missionTeamRepository.CreatedMissionTeam);
        Assert.Equal(10, created.MissionId);
        Assert.Equal(2, created.RescuerTeamId);
        Assert.Equal(MissionTeamExecutionStatus.Assigned.ToString(), created.Status);
        Assert.Equal(response.AssignedAt, created.AssignedAt);

        var sentCommand = Assert.IsType<ChangeTeamMissionStateCommand>(Assert.Single(mediator.SentRequests));
        Assert.Equal(2, sentCommand.TeamId);
        Assert.Equal("assign", sentCommand.Action);
    }

    private static RescueTeamModel BuildTeam(int id)
    {
        var team = RescueTeamModel.Create("Alpha Team", RescueTeamType.Rescue, 1, Guid.NewGuid());
        team.SetId(id);
        return team;
    }

    private sealed class StubMissionRepository(MissionModel? mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(mission);

        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> missionIds, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int missionId, MissionStatus status, bool isCompleted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMissionTeamRepository(int createdId = 0) : IMissionTeamRepository
    {
        public MissionTeamModel? CreatedMissionTeam { get; private set; }

        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default)
        {
            CreatedMissionTeam = model;
            return Task.FromResult(createdId);
        }

        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class StubRescueTeamRepository(RescueTeamModel? team) : IRescueTeamRepository
    {
        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(team);

        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<RescueTeamModel?>(null);
        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<RescueTeamModel>([], 0, pageNumber, pageSize));
        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult((new List<AgentTeamInfo>(), 0));
    }
}
