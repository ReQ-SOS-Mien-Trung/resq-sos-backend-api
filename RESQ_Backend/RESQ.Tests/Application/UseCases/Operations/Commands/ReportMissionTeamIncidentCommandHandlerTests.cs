using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class ReportMissionTeamIncidentCommandHandlerTests
{
    private static readonly Guid ReporterId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ─── Mission not found ────────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = BuildHandler(missionRepo: new StubMissionRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Mission not OnGoing ──────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenMissionNotOnGoing()
    {
        var mission = new MissionModel { Id = 10, Status = MissionStatus.Planned };
        var handler = BuildHandler(missionRepo: new StubMissionRepo(mission));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── MissionTeam not found ────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionTeamDoesNotExist()
    {
        var handler = BuildHandler(missionTeamRepo: new StubMissionTeamRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── MissionTeam does not belong to mission ───────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenMissionTeamMismatch()
    {
        var mismatchTeam = BuildMissionTeam(missionId: 999);
        var handler = BuildHandler(missionTeamRepo: new StubMissionTeamRepo(mismatchTeam));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Reporter not in team ─────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenReporterNotInTeam()
    {
        var teamNoReporter = BuildMissionTeam();
        teamNoReporter.RescueTeamMembers = []; // empty members
        var handler = BuildHandler(missionTeamRepo: new StubMissionTeamRepo(teamNoReporter));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── ContinueMission skips activity failure ───────────────────

    [Fact]
    public async Task Handle_ContinueMission_ReturnsWithoutFailingActivities()
    {
        var handler = BuildHandler();

        var command = BuildCommand(missionDecision: "continue_mission");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("continue_mission", result.DecisionCode);
        Assert.Equal("Reported", result.Status);
        Assert.True(result.IncidentId > 0);
    }

    // ─── HandoverMission resets assignments ───────────────────────

    [Fact]
    public async Task Handle_HandoverMission_ResetsAssignmentsToPlanned()
    {
        var activityRepo = new StubActivityRepo([BuildUnfinishedActivity()]);
        var handler = BuildHandler(activityRepo: activityRepo);

        var payload = BuildPayload(
            missionDecision: "handover_mission",
            handover: new MissionHandoverDto { NeedsMissionTakeover = true });

        var command = new ReportMissionTeamIncidentCommand(10, 1, payload, ReporterId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("handover_mission", result.DecisionCode);
        Assert.True(activityRepo.ResetCalls > 0);
    }

    // ─── RescueWholeTeam changes rescue team state ────────────────

    [Fact]
    public async Task Handle_RescueWholeTeam_ChangesRescueTeamState()
    {
        var leaderId = Guid.NewGuid();
        var rescueTeam = RESQ.Domain.Entities.Personnel.RescueTeamModel.Create(
            "Alpha", RESQ.Domain.Enum.Personnel.RescueTeamType.Rescue, 1, Guid.NewGuid());
        rescueTeam.AddMember(leaderId, isLeader: true, "Core", null);
        rescueTeam.SetAvailableByLeader(leaderId);
        rescueTeam.AssignMission();
        rescueTeam.StartMission();
        var rescueTeamRepo = new StubRescueTeamRepo(rescueTeam);
        var activityRepo = new StubActivityRepo([BuildUnfinishedActivity()]);

        var mediator = new RecordingMediator(r => r switch
        {
            RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest.CreateSosRequestCommand =>
                new RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest.CreateSosRequestResponse
                {
                    Id = 999, Status = "Pending", PriorityLevel = "Critical"
                },
            _ => null
        });

        var handler = BuildHandler(
            activityRepo: activityRepo,
            rescueTeamRepo: rescueTeamRepo,
            mediator: mediator);

        var payload = BuildPayload(
            missionDecision: "rescue_whole_team_immediately",
            rescueRequest: new MissionRescueRequestDto
            {
                SupportTypes = ["rescue_support"],
                Priority = "critical"
            });

        var command = new ReportMissionTeamIncidentCommand(10, 1, payload, ReporterId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("rescue_whole_team_immediately", result.DecisionCode);
        Assert.True(rescueTeamRepo.UpdateCalls > 0);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static ReportMissionTeamIncidentCommand BuildCommand(
        string missionDecision = "continue_mission") =>
        new(
            MissionId: 10,
            MissionTeamId: 1,
            Payload: BuildPayload(missionDecision: missionDecision),
            ReportedBy: ReporterId);

    private static MissionIncidentReportRequest BuildPayload(
        string missionDecision = "continue_mission",
        MissionRescueRequestDto? rescueRequest = null,
        MissionHandoverDto? handover = null) => new()
    {
        Scope = "Mission",
        Context = new MissionIncidentContextDto
        {
            MissionId = 10,
            MissionTeamId = 1,
            Location = new GeoLocationDto { Latitude = 10.76, Longitude = 106.66 }
        },
        MissionDecision = missionDecision,
        Note = "Test incident",
        RescueRequest = rescueRequest,
        Handover = handover
    };

    private static MissionTeamModel BuildMissionTeam(int id = 1, int missionId = 10) => new()
    {
        Id = id,
        MissionId = missionId,
        RescuerTeamId = 3,
        TeamName = "Alpha",
        Latitude = 10.76,
        Longitude = 106.66,
        RescueTeamMembers = [new MissionTeamMemberInfo { UserId = ReporterId, FullName = "Tester", Phone = "0901" }]
    };

    private static MissionActivityModel BuildUnfinishedActivity(int id = 100) => new()
    {
        Id = id,
        MissionId = 10,
        MissionTeamId = 1,
        Step = 1,
        ActivityType = "DELIVER_SUPPLIES",
        Status = MissionActivityStatus.OnGoing,
        SosRequestId = null
    };

    private static ReportMissionTeamIncidentCommandHandler BuildHandler(
        StubMissionRepo? missionRepo = null,
        StubMissionTeamRepo? missionTeamRepo = null,
        StubActivityRepo? activityRepo = null,
        StubRescueTeamRepo? rescueTeamRepo = null,
        RecordingMediator? mediator = null)
    {
        return new ReportMissionTeamIncidentCommandHandler(
            missionRepo ?? new StubMissionRepo(new MissionModel { Id = 10, Status = MissionStatus.OnGoing }),
            missionTeamRepo ?? new StubMissionTeamRepo(BuildMissionTeam()),
            new StubTeamIncidentRepo(),
            activityRepo ?? new StubActivityRepo([]),
            rescueTeamRepo ?? new StubRescueTeamRepo(
                RESQ.Domain.Entities.Personnel.RescueTeamModel.Create(
                    "Alpha", RESQ.Domain.Enum.Personnel.RescueTeamType.Rescue, 1, Guid.NewGuid())),
            new StubSosRequestRepo(),
            new StubSosPriorityRuleConfigRepo(),
            new StubSosRequestUpdateRepo(),
            mediator ?? new RecordingMediator(),
            new StubUnitOfWork(),
            NullLogger<ReportMissionTeamIncidentCommandHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    internal sealed class StubMissionRepo(MissionModel? mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(mission);
        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel m, Guid cid, CancellationToken ct = default) => Task.FromResult(m.Id);
        public Task UpdateAsync(MissionModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int mid, MissionStatus s, bool c, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class StubMissionTeamRepo(MissionTeamModel? team) : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(team);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel m, CancellationToken ct = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string s, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double lat, double lon, string src, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int mid, int rtid, CancellationToken ct = default) => Task.FromResult(team);
    }

    internal sealed class StubActivityRepo(List<MissionActivityModel> activities) : IMissionActivityRepository
    {
        public int ResetCalls { get; private set; }
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(activities.FirstOrDefault(a => a.Id == id));
        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default) => Task.FromResult(a.Id);
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult<IEnumerable<MissionActivityModel>>(activities.Where(a => a.MissionId == mid));
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int apId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task UpdateStatusAsync(int aid, MissionActivityStatus s, Guid db, string? img = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int aid, int mtid, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> aids, Guid db, CancellationToken ct = default) { ResetCalls++; return Task.CompletedTask; }
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class StubRescueTeamRepo(RESQ.Domain.Entities.Personnel.RescueTeamModel? team) : IRescueTeamRepository
    {
        public int UpdateCalls { get; private set; }
        public Task<RESQ.Domain.Entities.Personnel.RescueTeamModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(team);
        public Task<RESQ.Domain.Entities.Personnel.RescueTeamModel?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.Personnel.RescueTeamModel?>(null);
        public Task<PagedResult<RESQ.Domain.Entities.Personnel.RescueTeamModel>> GetPagedAsync(int pn, int ps, CancellationToken ct = default) => Task.FromResult(new PagedResult<RESQ.Domain.Entities.Personnel.RescueTeamModel>([], 0, pn, ps));
        public Task<bool> IsUserInActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> IsLeaderInActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid uid, string cc, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetTopAbilityCategoryAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task CreateAsync(RESQ.Domain.Entities.Personnel.RescueTeamModel t, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(RESQ.Domain.Entities.Personnel.RescueTeamModel t, CancellationToken ct = default) { UpdateCalls++; return Task.CompletedTask; }
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int apId, IEnumerable<int> excIds, CancellationToken ct = default) => Task.FromResult(0);
        public Task<(List<RESQ.Application.Services.AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? ak, bool? a, int p, int ps, CancellationToken ct = default) => Task.FromResult((new List<RESQ.Application.Services.AgentTeamInfo>(), 0));
    }

    internal sealed class StubTeamIncidentRepo : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RESQ.Application.Common.Models.PagedResult<TeamIncidentModel>> GetPagedByMissionIdAsync(int missionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<TeamIncidentModel?>(null);
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int mtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel m, CancellationToken ct = default) => Task.FromResult(1);
        public Task UpdateStatusAsync(int id, TeamIncidentStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? sosId, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class StubSosRequestRepo : ISosRequestRepository
    {
        public Task CreateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, System.Collections.Generic.IReadOnlyCollection<RESQ.Domain.Enum.Emergency.SosRequestStatus>? statuses = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<SosRequestModel?>(null);
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    internal sealed class StubSosPriorityRuleConfigRepo : ISosPriorityRuleConfigRepository
    {
        public Task<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?> GetAsync(CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?>(null);
        public Task<IReadOnlyList<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel>>([]);
        public Task<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?>(null);
        public Task<bool> ExistsConfigVersionAsync(string cv, int? eid = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task CreateAsync(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class StubSosRequestUpdateRepo : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }
}
