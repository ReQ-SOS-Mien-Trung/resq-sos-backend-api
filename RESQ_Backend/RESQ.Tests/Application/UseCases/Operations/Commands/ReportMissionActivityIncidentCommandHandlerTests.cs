using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class ReportMissionActivityIncidentCommandHandlerTests
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

    // ─── MissionTeam mismatch ─────────────────────────────────────

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
        teamNoReporter.RescueTeamMembers = [];
        var handler = BuildHandler(missionTeamRepo: new StubMissionTeamRepo(teamNoReporter));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Activity not found in mission ────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenActivityNotInMission()
    {
        // Activity 100 is expected in payload but we provide empty list
        var handler = BuildHandler(activityRepo: new StubActivityRepo([]));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Activity does not belong to team ─────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenActivityNotBelongToTeam()
    {
        var activity = BuildActivity(missionTeamId: 999); // wrong team
        var handler = BuildHandler(activityRepo: new StubActivityRepo([activity]));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── Activity status cannot be failed ─────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenActivityStatusCannotFail()
    {
        var activity = BuildActivity(status: MissionActivityStatus.Succeed);
        var handler = BuildHandler(activityRepo: new StubActivityRepo([activity]));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // ─── CannotContinueActivity → fails activities ────────────────

    [Fact]
    public async Task Handle_CannotContinueActivity_FailsActivities()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo([activity]));

        var payload = BuildPayload(canContinue: false, needReassign: false);
        var command = new ReportMissionActivityIncidentCommand(10, 1, payload, ReporterId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("cannot_continue_activity", result.DecisionCode);
        Assert.Equal("Reported", result.Status);
    }

    // ─── ReassignActivity → resets to Planned ─────────────────────

    [Fact]
    public async Task Handle_ReassignActivity_ResetsToPlanned()
    {
        var activity = BuildActivity();
        var activityRepo = new StubActivityRepo([activity]);
        var mediator = new RecordingMediator(r => r switch
        {
            RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest.CreateSosRequestCommand =>
                new RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest.CreateSosRequestResponse
                {
                    Id = 999, Status = "Pending", PriorityLevel = "High"
                },
            _ => null
        });
        var handler = BuildHandler(activityRepo: activityRepo, mediator: mediator);

        var payload = BuildPayload(canContinue: false, needReassign: true);
        var command = new ReportMissionActivityIncidentCommand(10, 1, payload, ReporterId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("reassign_activity", result.DecisionCode);
        Assert.True(activityRepo.ResetCalls > 0);
    }

    // ─── ContinueActivity → no workload impact ───────────────────

    [Fact]
    public async Task Handle_ContinueActivity_NoWorkloadImpact()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(activityRepo: new StubActivityRepo([activity]));

        var payload = BuildPayload(canContinue: true, needReassign: false);
        var command = new ReportMissionActivityIncidentCommand(10, 1, payload, ReporterId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("continue_activity", result.DecisionCode);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static ReportMissionActivityIncidentCommand BuildCommand() =>
        new(
            MissionId: 10,
            MissionTeamId: 1,
            Payload: BuildPayload(),
            ReportedBy: ReporterId);

    private static ActivityIncidentReportRequest BuildPayload(
        bool canContinue = false,
        bool needReassign = false,
        bool needSos = false) => new()
    {
        Scope = "Activity",
        Context = new ActivityIncidentContextDto
        {
            MissionId = 10,
            MissionTeamId = 1,
            Location = new GeoLocationDto { Latitude = 10.76, Longitude = 106.66 },
            Activities = [new ActivitySnapshotDto { ActivityId = 100, ActivityType = "DELIVER_SUPPLIES", Step = 1 }]
        },
        Impact = new ActivityImpactDto
        {
            CanContinueActivity = canContinue,
            NeedReassignActivity = needReassign,
            NeedSupportSOS = needSos
        },
        Note = "Activity incident test"
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

    private static MissionActivityModel BuildActivity(
        int id = 100,
        int missionTeamId = 1,
        MissionActivityStatus status = MissionActivityStatus.OnGoing) => new()
    {
        Id = id,
        MissionId = 10,
        MissionTeamId = missionTeamId,
        Step = 1,
        ActivityType = "DELIVER_SUPPLIES",
        Status = status,
        SosRequestId = null
    };

    private static ReportMissionActivityIncidentCommandHandler BuildHandler(
        StubMissionRepo? missionRepo = null,
        StubMissionTeamRepo? missionTeamRepo = null,
        StubActivityRepo? activityRepo = null,
        RecordingMediator? mediator = null)
    {
        return new ReportMissionActivityIncidentCommandHandler(
            missionRepo ?? new StubMissionRepo(new MissionModel { Id = 10, Status = MissionStatus.OnGoing }),
            activityRepo ?? new StubActivityRepo([BuildActivity()]),
            missionTeamRepo ?? new StubMissionTeamRepo(BuildMissionTeam()),
            new StubTeamIncidentRepo(),
            new StubSosRequestRepo(),
            new StubSosPriorityRuleConfigRepo(),
            new StubSosRequestUpdateRepo(),
            new StubDepotRepo(),
            mediator ?? new RecordingMediator(),
            new StubUnitOfWork(),
            NullLogger<ReportMissionActivityIncidentCommandHandler>.Instance);
    }

    // ─── Stubs (reuse contracts from team incident tests) ─────────

    private sealed class StubMissionRepo(MissionModel? mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(mission);
        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel m, Guid cid, CancellationToken ct = default) => Task.FromResult(m.Id);
        public Task UpdateAsync(MissionModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int mid, MissionStatus s, bool c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubMissionTeamRepo(MissionTeamModel? team) : IMissionTeamRepository
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

    private sealed class StubActivityRepo(List<MissionActivityModel> activities) : IMissionActivityRepository
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

    private sealed class StubTeamIncidentRepo : ITeamIncidentRepository
    {
        public Task<IEnumerable<TeamIncidentModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<TeamIncidentModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<TeamIncidentModel?>(null);
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<IEnumerable<TeamIncidentModel>> GetByMissionTeamIdAsync(int mtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<TeamIncidentModel>());
        public Task<int> CreateAsync(TeamIncidentModel m, CancellationToken ct = default) => Task.FromResult(1);
        public Task UpdateStatusAsync(int id, TeamIncidentStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateSupportSosRequestIdAsync(int id, int? sosId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSosRequestRepo : ISosRequestRepository
    {
        public Task CreateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SosRequestModel sos, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<SosRequestModel?>(null);
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
        public Task UpdateStatusAsync(int id, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusByClusterIdAsync(int cid, SosRequestStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<SosRequestModel>());
    }

    private sealed class StubSosPriorityRuleConfigRepo : ISosPriorityRuleConfigRepository
    {
        public Task<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?> GetAsync(CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?>(null);
        public Task<IReadOnlyList<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel>>([]);
        public Task<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.System.SosPriorityRuleConfigModel?>(null);
        public Task<bool> ExistsConfigVersionAsync(string cv, int? eid = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task CreateAsync(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSosRequestUpdateRepo : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel u, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyCollection<int>>>(new Dictionary<int, IReadOnlyCollection<int>>());
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>>(new Dictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>());
    }

    private sealed class StubDepotRepo : IDepotInventoryRepository
    {
        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken ct = default) => Task.FromResult(new List<SupplyShortageResult>());
        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.FromResult(new MissionSupplyReservationResult());
        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int?> GetActiveDepotIdByManagerAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<int?>(null);
        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task<PagedResult<RESQ.Domain.Entities.Logistics.InventoryItemModel>> GetInventoryPagedAsync(int d, List<int>? c, List<RESQ.Domain.Enum.Logistics.ItemType>? it, List<RESQ.Domain.Enum.Logistics.TargetGroup>? tg, string? n, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PagedResult<RESQ.Domain.Entities.Logistics.Models.InventoryLotModel>> GetInventoryLotsAsync(int d, int i, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory.DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(List<RESQ.Application.Services.AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string ck, string? tk, int p, int ps, IReadOnlyCollection<int>? adids = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int d, CancellationToken ct = default) => Task.FromResult<(double, double)?>(null);
        public Task<(List<RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems.WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? ids, Dictionary<int, int> q, bool a, int? e, int pn, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int d, List<(int ItemModelId, int Quantity)> i, Guid pb, int aid, int mid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int d, int mid, int aid, Guid pb, List<(int ItemModelId, int Quantity, DateTime? ExpiredDate)> ci, List<(int ReusableItemId, string? Condition, string? Note)> ri, List<(int ItemModelId, int Quantity)> lrq, string? dn, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems.LowStockRawItemDto>> GetLowStockRawItemsAsync(int? d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ExportInventoryAsync(int d, int i, int q, Guid pb, string? n, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int d, int i, int qc, Guid pb, string r, string? n, DateTime? e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int s, int t, int c, Guid pb, int? lp = null, int bs = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int s, int t, int c, int tid, Guid pb, IReadOnlyCollection<RESQ.Application.Repositories.Logistics.DepotClosureTransferItemMoveDto> i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int d, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task ZeroOutForClosureAsync(int d, int c, Guid pb, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasActiveInventoryCommitmentsAsync(int d, CancellationToken ct = default) => Task.FromResult(false);
        public Task DisposeConsumableLotAsync(int lotId, int quantity, string reason, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DecommissionReusableItemAsync(int reusableItemId, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<RESQ.Domain.Entities.Logistics.Models.ExpiringLotModel>> GetExpiringLotsAsync(int depotId, int daysAhead, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Domain.Entities.Logistics.Models.ExpiringLotModel>());
    }
}
