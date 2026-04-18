using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class AddMissionActivityCommandHandlerTests
{
    private static readonly Guid CoordinatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    // --- Mission not found ----------------------------------------

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionDoesNotExist()
    {
        var handler = BuildHandler(missionRepo: new StubMissionRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(), CancellationToken.None));
    }

    // --- Check inventory with buffer, shortage throws BadRequest --

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenSupplyShortage()
    {
        var shortages = new List<SupplyShortageResult>
        {
            new() { ItemModelId = 100, ItemName = "Water", RequestedQuantity = 11, AvailableQuantity = 5, NotFound = false }
        };
        var depotRepo = new StubDepotInventoryRepository { ShortagesToReturn = shortages };

        var handler = BuildHandler(depotInventoryRepo: depotRepo);
        var command = BuildCommand(depotId: 1, supplies:
        [
            new SuggestedSupplyItemDto { Id = 100, Name = "Water", Quantity = 10, BufferRatio = 0.10 }
        ]);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("Water", ex.Message);
        Assert.Contains("11", ex.Message);
    }

    // --- Creates activity as Planned ------------------------------

    [Fact]
    public async Task Handle_CreatesActivityWithPlannedStatus()
    {
        var activityRepo = new StubMissionActivityRepository();
        var handler = BuildHandler(activityRepo: activityRepo);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(1, result.ActivityId);
        Assert.Equal(10, result.MissionId);
    }

    // --- With RescueTeamId, calls assign team ---------------------

    [Fact]
    public async Task Handle_WithRescueTeamId_CallsMediator()
    {
        var mediator = new RecordingMediator(r => r switch
        {
            AssignTeamToActivityCommand => new AssignTeamToActivityResponse
            {
                ActivityId = 1,
                MissionTeamId = 5,
                RescueTeamId = 3
            },
            _ => null
        });

        var handler = BuildHandler(mediator: mediator);
        var command = BuildCommand(rescueTeamId: 3);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(5, result.MissionTeamId);
        Assert.Equal(3, result.AssignedRescueTeamId);
        Assert.Contains(mediator.SentRequests, r => r is AssignTeamToActivityCommand);
    }

    // --- Reserve supplies success syncs snapshot ------------------

    [Fact]
    public async Task Handle_ReserveSuppliesSuccess_SyncsSnapshot()
    {
        var depotRepo = new StubDepotInventoryRepository();
        var activityRepo = new StubMissionActivityRepository();
        var uow = new StubUnitOfWork();

        var handler = BuildHandler(
            activityRepo: activityRepo,
            depotInventoryRepo: depotRepo,
            unitOfWork: uow);

        var command = BuildCommand(depotId: 1, supplies:
        [
            new SuggestedSupplyItemDto { Id = 100, Name = "Water", Quantity = 10 }
        ]);

        await handler.Handle(command, CancellationToken.None);

        Assert.True(depotRepo.ReserveCalls > 0);
        Assert.True(uow.SaveCalls >= 2); // initial save + reserve sync
    }

    // --- Reserve fail does not crash ------------------------------

    [Fact]
    public async Task Handle_ReserveFail_DoesNotCrash()
    {
        var depotRepo = new StubDepotInventoryRepository { ThrowOnReserve = true };
        var handler = BuildHandler(depotInventoryRepo: depotRepo);

        var command = BuildCommand(depotId: 1, supplies:
        [
            new SuggestedSupplyItemDto { Id = 100, Name = "Water", Quantity = 10 }
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.ActivityId); // still succeeds
    }

    [Fact]
    public async Task Handle_EnrichesDescription_WithVictimSummary()
    {
        var activityRepo = new StubMissionActivityRepository();
        var handler = BuildHandler(
            activityRepo: activityRepo,
            sosRequestRepo: new StubSosRequestRepository(
                new SosRequestModel
                {
                    Id = 55,
                    StructuredData =
                        """
                        {
                          "incident": {},
                          "victims": [
                            { "person_type": "CHILD", "custom_name": "Khoa" },
                            { "person_type": "ELDERLY", "custom_name": "Chu" }
                          ]
                        }
                        """
                }));

        var command = BuildCommand();
        command = command with { SosRequestId = 55 };

        await handler.Handle(command, CancellationToken.None);

        var savedActivity = await activityRepo.GetByIdAsync(1, CancellationToken.None);
        Assert.NotNull(savedActivity);
        Assert.Contains("Đối tượng cần hỗ trợ: Khoa (trẻ em), Chu (người già).", savedActivity!.Description);
    }

    // --- Helpers --------------------------------------------------

    private static AddMissionActivityCommand BuildCommand(
        int missionId = 10,
        int? depotId = null,
        int? rescueTeamId = null,
        List<SuggestedSupplyItemDto>? supplies = null) => new(
        MissionId: missionId,
        Step: 1,
        ActivityType: "DELIVER_SUPPLIES",
        Description: "Deliver supplies to victims",
        Priority: "High",
        EstimatedTime: 60,
        SosRequestId: null,
        DepotId: depotId,
        DepotName: depotId.HasValue ? "Kho 1" : null,
        DepotAddress: null,
        SuppliesToCollect: supplies,
        Target: "Khu vực ngập",
        TargetLatitude: 10.76,
        TargetLongitude: 106.66,
        RescueTeamId: rescueTeamId,
        AssignedById: CoordinatorId);

    private static AddMissionActivityCommandHandler BuildHandler(
        StubMissionRepository? missionRepo = null,
        StubMissionActivityRepository? activityRepo = null,
        StubDepotInventoryRepository? depotInventoryRepo = null,
        IMediator? mediator = null,
        StubUnitOfWork? unitOfWork = null,
        StubSosRequestRepository? sosRequestRepo = null,
        StubSosRequestUpdateRepository? sosRequestUpdateRepo = null)
    {
        return new AddMissionActivityCommandHandler(
            missionRepo ?? new StubMissionRepository(new MissionModel { Id = 10, Status = MissionStatus.Planned }),
            activityRepo ?? new StubMissionActivityRepository(),
            new StubMissionTeamRepository(),
            new StubRescueTeamRepository(),
            sosRequestRepo ?? new StubSosRequestRepository(),
            sosRequestUpdateRepo ?? new StubSosRequestUpdateRepository(),
            depotInventoryRepo ?? new StubDepotInventoryRepository(),
            new StubDepotRepository(),
            mediator ?? new RecordingMediator(),
            unitOfWork ?? new StubUnitOfWork(),
            NullLogger<AddMissionActivityCommandHandler>.Instance);
    }

    // --- Stubs ----------------------------------------------------

    private sealed class StubMissionRepository(MissionModel? mission) : IMissionRepository
    {
        public Task<MissionModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(mission);
        public Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int cid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<IEnumerable<MissionModel>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionModel>());
        public Task<int> CreateAsync(MissionModel m, Guid cid, CancellationToken ct = default) => Task.FromResult(m.Id);
        public Task UpdateAsync(MissionModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int mid, MissionStatus s, bool c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubMissionActivityRepository : IMissionActivityRepository
    {
        private readonly List<MissionActivityModel> _store = [];
        private int _nextId = 1;

        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default)
        {
            a.Id = _nextId++;
            _store.Add(a);
            return Task.FromResult(a.Id);
        }
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_store.FirstOrDefault(a => a.Id == id));
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<MissionActivityModel>>(_store.Where(a => a.MissionId == mid));
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int apId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task UpdateStatusAsync(int aid, MissionActivityStatus s, Guid db, string? img = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task AssignTeamAsync(int aid, int mtid, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> aids, Guid db, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSosRequestRepository(params SosRequestModel[] requests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _requests = requests.ToDictionary(request => request.Id);

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_requests.GetValueOrDefault(id));

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusByClusterIdAsync(int clusterId, RESQ.Domain.Enum.Emergency.SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubMissionTeamRepository : IMissionTeamRepository
    {
        public Task<MissionTeamModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<MissionTeamModel?>(null);
        public Task<IEnumerable<MissionTeamModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<int> CreateAsync(MissionTeamModel m, CancellationToken ct = default) => Task.FromResult(0);
        public Task UpdateStatusAsync(int id, string s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(int id, string s, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateCurrentLocationAsync(int id, double lat, double lon, string src, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionTeamModel>> GetActiveByRescuerTeamIdAsync(int rtid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionTeamModel>());
        public Task<MissionTeamModel?> GetByMissionAndTeamAsync(int mid, int rtid, CancellationToken ct = default) => Task.FromResult<MissionTeamModel?>(null);
    }

    private sealed class StubRescueTeamRepository : IRescueTeamRepository
    {
        public Task<RESQ.Domain.Entities.Personnel.RescueTeamModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.Personnel.RescueTeamModel?>(null);
        public Task<RESQ.Domain.Entities.Personnel.RescueTeamModel?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<RESQ.Domain.Entities.Personnel.RescueTeamModel?>(null);
        public Task<PagedResult<RESQ.Domain.Entities.Personnel.RescueTeamModel>> GetPagedAsync(int pn, int ps, CancellationToken ct = default) => Task.FromResult(new PagedResult<RESQ.Domain.Entities.Personnel.RescueTeamModel>([], 0, pn, ps));
        public Task<bool> IsUserInActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> IsLeaderInActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Guid?> GetTeamLeaderUserIdByMemberAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task<bool> SoftRemoveMemberFromActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasRequiredAbilityCategoryAsync(Guid uid, string cc, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetTopAbilityCategoryAsync(Guid uid, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task CreateAsync(RESQ.Domain.Entities.Personnel.RescueTeamModel t, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(RESQ.Domain.Entities.Personnel.RescueTeamModel t, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountActiveTeamsByAssemblyPointAsync(int apId, IEnumerable<int> excIds, CancellationToken ct = default) => Task.FromResult(0);
        public Task<(List<RESQ.Application.Services.AgentTeamInfo> Teams, int TotalCount)> GetTeamsForAgentAsync(string? ak, bool? a, int p, int ps, CancellationToken ct = default) => Task.FromResult((new List<RESQ.Application.Services.AgentTeamInfo>(), 0));
    }

        private sealed class StubDepotRepository : RESQ.Application.Repositories.Logistics.IDepotRepository
    {
        public Task<RESQ.Domain.Enum.Logistics.DepotStatus?> GetStatusByIdAsync(int id, CancellationToken token = default) => Task.FromResult<RESQ.Domain.Enum.Logistics.DepotStatus?>(RESQ.Domain.Enum.Logistics.DepotStatus.Available);
        public Task<RESQ.Domain.Entities.Logistics.DepotModel?> GetByIdAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<RESQ.Domain.Entities.Logistics.DepotModel?> GetByNameAsync(string name, CancellationToken token = default) => throw new NotImplementedException();
        public Task<int> GetActiveDepotCountExcludingAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task CreateAsync(RESQ.Domain.Entities.Logistics.DepotModel d, CancellationToken token = default) => throw new NotImplementedException();
        public Task UpdateAsync(RESQ.Domain.Entities.Logistics.DepotModel d, CancellationToken token = default) => Task.CompletedTask;
        public Task AssignManagerAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, Guid newManagerId, Guid? assignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignManagerAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnassignSpecificManagersAsync(RESQ.Domain.Entities.Logistics.DepotModel depot, IReadOnlyList<Guid> userIds, Guid? unassignedBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RESQ.Application.Common.Models.PagedResult<RESQ.Domain.Entities.Logistics.DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, System.Collections.Generic.IEnumerable<RESQ.Domain.Enum.Logistics.DepotStatus>? statuses = null, string? search = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.IEnumerable<RESQ.Domain.Entities.Logistics.DepotModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.IEnumerable<RESQ.Domain.Entities.Logistics.DepotModel>> GetAvailableDepotsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int, int)> GetNonTerminalSupplyRequestCountsAsync(int id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<decimal> GetConsumableTransferVolumeAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(int AvailableCount, int InUseCount)> GetReusableItemCountsAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetConsumableInventoryRowCountAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsManagerActiveElsewhereAsync(Guid managerId, int excludeDepotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.List<RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure.ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<System.Collections.Generic.List<RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure.ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int depotId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>> GetDepotManagersAsync(int depotId, CancellationToken cancellationToken = default) => Task.FromResult(new List<RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers.DepotManagerInfoDto>());
    }
    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public List<SupplyShortageResult> ShortagesToReturn { get; set; } = [];
        public bool ThrowOnReserve { get; set; }
        public int ReserveCalls { get; private set; }

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId,
            List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken ct = default)
            => Task.FromResult(ShortagesToReturn);

        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId,
            List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default)
        {
            ReserveCalls++;
            if (ThrowOnReserve) throw new Exception("Reserve failed");
            return Task.FromResult(new MissionSupplyReservationResult());
        }

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.CompletedTask;

        // Unused methods
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
        public Task DisposeConsumableLotAsync(int lotId, int quantity, string reason, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DecommissionReusableItemAsync(int reusableItemId, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ExpiringLotModel>> GetExpiringLotsAsync(int depotId, int daysAhead, CancellationToken cancellationToken = default) => Task.FromResult(new List<ExpiringLotModel>());
        public Task<List<RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems.LowStockRawItemDto>> GetLowStockRawItemsAsync(int? d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ExportInventoryAsync(int d, int i, int q, Guid pb, string? n, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int d, int i, int qc, Guid pb, string r, string? n, DateTime? e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int s, int t, int c, Guid pb, int? lp = null, int bs = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int s, int t, int c, int tid, Guid pb, IReadOnlyCollection<RESQ.Application.Repositories.Logistics.DepotClosureTransferItemMoveDto> i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int d, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task ZeroOutForClosureAsync(int d, int c, Guid pb, string? n, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasActiveInventoryCommitmentsAsync(int d, CancellationToken ct = default) => Task.FromResult(false);
    }
}



