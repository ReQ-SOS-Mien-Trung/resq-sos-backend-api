using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;
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

    [Fact]
    public async Task Handle_SyncsAssignedRescueTeamsToOnMission_WhenMissionStarts()
    {
        var rescueTeamRepository = new StubRescueTeamRepository(
            BuildTeam(6, RescueTeamStatus.Assigned),
            BuildTeam(7, RescueTeamStatus.OnMission));
        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 15, Status = MissionStatus.Planned }),
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(
                new MissionTeamModel { Id = 21, RescuerTeamId = 6 },
                new MissionTeamModel { Id = 22, RescuerTeamId = 7 }),
            new StubSosRequestRepository(),
            new StubUnitOfWork(),
            rescueTeamRepository: rescueTeamRepository);

        await handler.Handle(new UpdateMissionStatusCommand(15, MissionStatus.OnGoing, DecisionById), CancellationToken.None);

        Assert.Equal(RescueTeamStatus.OnMission, rescueTeamRepository.Get(6).Status);
        Assert.Equal(RescueTeamStatus.OnMission, rescueTeamRepository.Get(7).Status);
        Assert.Equal([6], rescueTeamRepository.UpdatedTeamIds);
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenAnyRescueTeamIsNotAssignedDuringMissionStart()
    {
        var rescueTeamRepository = new StubRescueTeamRepository(
            BuildTeam(6, RescueTeamStatus.Assigned),
            BuildTeam(7, RescueTeamStatus.Available));
        var unitOfWork = new StubUnitOfWork();
        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 15, Status = MissionStatus.Planned }),
            new StubMissionActivityRepository(),
            new StubMissionTeamRepository(
                new MissionTeamModel { Id = 21, RescuerTeamId = 6 },
                new MissionTeamModel { Id = 22, RescuerTeamId = 7 }),
            new StubSosRequestRepository(),
            unitOfWork,
            rescueTeamRepository: rescueTeamRepository);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new UpdateMissionStatusCommand(15, MissionStatus.OnGoing, DecisionById), CancellationToken.None));

        Assert.Contains("Đội #7", exception.Message);
        Assert.Equal(0, unitOfWork.SaveCalls);
        Assert.Empty(rescueTeamRepository.UpdatedTeamIds);
        Assert.Equal(RescueTeamStatus.Assigned, rescueTeamRepository.Get(6).Status);
    }

    [Fact]
    public async Task Handle_ChecksOutAcceptedTeamMembers_WhenMissionStarts()
    {
        var acceptedMemberId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var removedMemberId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
        var assemblyEventRepository = new StubAssemblyEventRepository();
        assemblyEventRepository.SetActiveEvent(assemblyPointId: 5, eventId: 50);

        var missionTeamRepository = new StubMissionTeamRepository(new MissionTeamModel
        {
            Id = 21,
            AssemblyPointId = 5,
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo { UserId = acceptedMemberId, Status = TeamMemberStatus.Accepted.ToString() },
                new MissionTeamMemberInfo { UserId = removedMemberId, Status = TeamMemberStatus.Removed.ToString() }
            ]
        });

        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 15, Status = MissionStatus.Planned }),
            new StubMissionActivityRepository(),
            missionTeamRepository,
            new StubSosRequestRepository(),
            new StubUnitOfWork(),
            assemblyEventRepository);

        await handler.Handle(new UpdateMissionStatusCommand(15, MissionStatus.OnGoing, DecisionById), CancellationToken.None);

        var checkout = Assert.Single(assemblyEventRepository.CheckOutCalls);
        Assert.Equal(50, checkout.EventId);
        Assert.Equal(acceptedMemberId, checkout.RescuerId);
    }

    [Fact]
    public async Task Handle_SkipsCheckout_WhenTeamHasNoActiveAssemblyEvent()
    {
        var assemblyEventRepository = new StubAssemblyEventRepository();
        var missionTeamRepository = new StubMissionTeamRepository(new MissionTeamModel
        {
            Id = 21,
            AssemblyPointId = 5,
            RescueTeamMembers =
            [
                new MissionTeamMemberInfo
                {
                    UserId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                    Status = TeamMemberStatus.Accepted.ToString()
                }
            ]
        });

        var handler = BuildHandler(
            new StubMissionRepository(new MissionModel { Id = 15, Status = MissionStatus.Planned }),
            new StubMissionActivityRepository(),
            missionTeamRepository,
            new StubSosRequestRepository(),
            new StubUnitOfWork(),
            assemblyEventRepository);

        await handler.Handle(new UpdateMissionStatusCommand(15, MissionStatus.OnGoing, DecisionById), CancellationToken.None);

        Assert.Empty(assemblyEventRepository.CheckOutCalls);
    }

    private static UpdateMissionStatusCommandHandler BuildHandler(
        IMissionRepository missionRepository,
        IMissionActivityRepository missionActivityRepository,
        IMissionTeamRepository missionTeamRepository,
        ISosRequestRepository sosRequestRepository,
        StubUnitOfWork unitOfWork,
        IAssemblyEventRepository? assemblyEventRepository = null,
        StubRescueTeamRepository? rescueTeamRepository = null,
        StubOperationalHubService? operationalHubService = null)
    {
        rescueTeamRepository ??= new StubRescueTeamRepository();
        operationalHubService ??= new StubOperationalHubService();

        var lifecycleSyncService = new RescueTeamMissionLifecycleSyncService(
            rescueTeamRepository,
            missionTeamRepository,
            operationalHubService,
            new StubAdminRealtimeHubService(),
            NullLogger<RescueTeamMissionLifecycleSyncService>.Instance);

        return new(
            missionRepository,
            missionActivityRepository,
            missionTeamRepository,
            sosRequestRepository,
            unitOfWork,
            NullLogger<UpdateMissionStatusCommandHandler>.Instance,
            assemblyEventRepository ?? new StubAssemblyEventRepository(),
            new StubAdminRealtimeHubService(),
            lifecycleSyncService);
    }

    private static RescueTeamModel BuildTeam(int id, RescueTeamStatus status)
    {
        var leaderId = Guid.Parse($"11111111-1111-1111-1111-{id:D12}");
        var team = RescueTeamModel.Create($"Team {id}", RescueTeamType.Rescue, id, Guid.NewGuid());
        team.SetId(id);
        team.AddMember(leaderId, isLeader: true, rescuerType: "Core", roleInTeam: "LEADER");
        team.SetAvailableByLeader(leaderId);

        switch (status)
        {
            case RescueTeamStatus.Available:
                return team;
            case RescueTeamStatus.Assigned:
                team.AssignMission();
                return team;
            case RescueTeamStatus.OnMission:
                team.AssignMission();
                team.StartMission();
                return team;
            default:
                throw new InvalidOperationException($"Unsupported test status: {status}");
        }
    }

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

    private sealed class StubMissionTeamRepository(params MissionTeamModel[] missionTeams) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default) => Task.FromResult(missionTeams.AsEnumerable());
        public Task<int> CreateAsync(MissionTeamModel model, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string status, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double latitude, double longitude, string locationSource, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int missionId, int rescuerTeamId, CancellationToken cancellationToken = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class StubRescueTeamRepository(params RescueTeamModel[] teams) : IRescueTeamRepository
    {
        private readonly Dictionary<int, RescueTeamModel> _teams = teams.ToDictionary(team => team.Id);

        public List<int> UpdatedTeamIds { get; } = [];

        public RescueTeamModel Get(int teamId) => _teams[teamId];

        public Task<RescueTeamModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.TryGetValue(id, out var team) ? team : null);

        public Task UpdateAsync(RescueTeamModel team, CancellationToken cancellationToken = default)
        {
            _teams[team.Id] = team;
            UpdatedTeamIds.Add(team.Id);
            return Task.CompletedTask;
        }

        public Task<RescueTeamModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RescueTeamModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsUserInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsLeaderInActiveTeamAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid memberUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid userId, string categoryCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GetTopAbilityCategoryAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(RescueTeamModel team, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int assemblyPointId, IEnumerable<int> excludeTeamIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(List<AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? abilityKeyword, bool? available, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
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
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SosRequestModel>([], 0, pageNumber, pageSize));
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubAssemblyEventRepository : IAssemblyEventRepository
    {
        private readonly Dictionary<int, (int EventId, string Status)> _activeEvents = [];

        public List<(int EventId, Guid RescuerId)> CheckOutCalls { get; } = [];

        public void SetActiveEvent(int assemblyPointId, int eventId)
            => _activeEvents[assemblyPointId] = (eventId, AssemblyEventStatus.Gathering.ToString());

        public Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult(_activeEvents.TryGetValue(assemblyPointId, out var value) ? value : ((int EventId, string Status)?)null);

        public Task<bool> CheckOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default)
        {
            CheckOutCalls.Add((eventId, rescuerId));
            return Task.FromResult(true);
        }

        public Task<bool> CheckOutVoluntaryAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, DateTime checkInDeadline, Guid createdBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ReturnCheckInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasParticipantCheckedOutAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<PagedResult<CheckedInRescuerDto>> GetCheckedInRescuersAsync(int eventId, int pageNumber, int pageSize, RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null, string? abilitySubgroupCode = null, string? abilityCategoryCode = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateEventStatusAsync(int eventId, string status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetParticipantIdsAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)?> GetEventByIdAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid?> GetEventCreatedByAsync(int eventId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> MarkParticipantAbsentAsync(int eventId, Guid rescuerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<PagedResult<MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UpcomingAssemblyEventDto>> GetUpcomingEventsForRescuerAsync(Guid rescuerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsWithExpiredDeadlineAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<int>> GetGatheringEventsExpiredAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CompleteEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> AutoMarkAbsentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
