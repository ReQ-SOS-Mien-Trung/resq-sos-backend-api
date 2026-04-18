using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateMissionActivityCommandHandlerTests
{
    // ─── Activity not found ───────────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenActivityDoesNotExist()
    {
        var handler = BuildHandler(activityRepo: new StubActivityRepo(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(BuildCommand(activityId: 999), CancellationToken.None));
    }

    // ─── Only Planned is allowed ──────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenStatusNotPlanned()
    {
        var activity = BuildActivity(status: MissionActivityStatus.OnGoing, activityType: "DELIVER_SUPPLIES");
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(step: 2), CancellationToken.None));
    }

    // ─── RETURN_ASSEMBLY_POINT OnGoing can only change AssemblyPointId/Description ─

    [Fact]
    public async Task Handle_ReturnAssemblyPoint_OnGoing_CanUpdateAssemblyPointId()
    {
        var activity = BuildActivity(status: MissionActivityStatus.OnGoing, activityType: "RETURN_ASSEMBLY_POINT");
        var assemblyPoint = new AssemblyPointModel
        {
            Id = 5, Name = "Điểm A", Location = new GeoLocation(10.76, 106.66)
        };

        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            assemblyPointRepo: new StubAssemblyPointRepo(assemblyPoint));

        var result = await handler.Handle(
            new UpdateMissionActivityCommand(
                ActivityId: 1, Step: null, ActivityType: null,
                Description: "Updated desc", Target: null, Items: null,
                AssemblyPointId: 5, TargetLatitude: null, TargetLongitude: null),
            CancellationToken.None);

        Assert.Equal(5, result.AssemblyPointId);
        Assert.Equal("Điểm A", result.AssemblyPointName);
    }

    [Fact]
    public async Task Handle_ReturnAssemblyPoint_OnGoing_CannotChangeStep()
    {
        var activity = BuildActivity(status: MissionActivityStatus.OnGoing, activityType: "RETURN_ASSEMBLY_POINT");
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(BuildCommand(step: 5), CancellationToken.None));
    }

    // ─── Release old reservations, validate new supplies ──────────

    [Fact]
    public async Task Handle_ReleasesOldReservations_ValidatesNewSupplies()
    {
        var activity = BuildActivity();
        activity.DepotId = 1;
        activity.Items = """[{"ItemId":100,"ItemName":"Water","Quantity":10}]""";

        var depotRepo = new StubDepotRepo();
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity), depotRepo: depotRepo);

        var newItems = """[{"ItemId":200,"ItemName":"Rice","Quantity":5}]""";
        var result = await handler.Handle(
            new UpdateMissionActivityCommand(
                ActivityId: 1, Step: null, ActivityType: null,
                Description: null, Target: null, Items: newItems,
                AssemblyPointId: null, TargetLatitude: null, TargetLongitude: null),
            CancellationToken.None);

        Assert.True(depotRepo.ReleaseCalls > 0);
        Assert.True(depotRepo.CheckCalls > 0);
    }

    // ─── Supply shortage on update throws BadRequest ──────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenNewSuppliesShortage()
    {
        var activity = BuildActivity();
        activity.DepotId = 1;

        var depotRepo = new StubDepotRepo
        {
            ShortagesToReturn = [new SupplyShortageResult { ItemModelId = 200, ItemName = "Rice", RequestedQuantity = 100, AvailableQuantity = 5 }]
        };
        var handler = BuildHandler(activityRepo: new StubActivityRepo(activity), depotRepo: depotRepo);

        var newItems = """[{"ItemId":200,"ItemName":"Rice","Quantity":100}]""";

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateMissionActivityCommand(
                    ActivityId: 1, Step: null, ActivityType: null,
                    Description: null, Target: null, Items: newItems,
                    AssemblyPointId: null, TargetLatitude: null, TargetLongitude: null),
                CancellationToken.None));
    }

    // ─── Assembly point not found ─────────────────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenAssemblyPointNotFound()
    {
        var activity = BuildActivity();
        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            assemblyPointRepo: new StubAssemblyPointRepo(null));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateMissionActivityCommand(
                    ActivityId: 1, Step: null, ActivityType: null,
                    Description: null, Target: null, Items: null,
                    AssemblyPointId: 99, TargetLatitude: null, TargetLongitude: null),
                CancellationToken.None));
    }

    // ─── Assembly point without coordinates ───────────────────────

    [Fact]
    public async Task Handle_ThrowsBadRequest_WhenAssemblyPointHasNoCoordinates()
    {
        var activity = BuildActivity();
        var ap = new AssemblyPointModel { Id = 5, Name = "NoCoords", Location = null };
        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            assemblyPointRepo: new StubAssemblyPointRepo(ap));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateMissionActivityCommand(
                    ActivityId: 1, Step: null, ActivityType: null,
                    Description: null, Target: null, Items: null,
                    AssemblyPointId: 5, TargetLatitude: null, TargetLongitude: null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReappliesVictimSummary_ToDescription()
    {
        var activity = BuildActivity(activityType: "RESCUE");
        activity.SosRequestId = 77;
        var handler = BuildHandler(
            activityRepo: new StubActivityRepo(activity),
            sosRequestRepo: new StubSosRequestRepo(
                new SosRequestModel
                {
                    Id = 77,
                    StructuredData =
                        """
                        {
                          "incident": {},
                          "victims": [
                            { "person_type": "CHILD", "custom_name": "Khoa" }
                          ]
                        }
                        """
                }));

        var result = await handler.Handle(
            new UpdateMissionActivityCommand(
                ActivityId: 1,
                Step: null,
                ActivityType: null,
                Description: "Tiếp cận mái nhà",
                Target: null,
                Items: null,
                AssemblyPointId: null,
                TargetLatitude: null,
                TargetLongitude: null),
            CancellationToken.None);

        Assert.Contains("Đối tượng cần hỗ trợ: Khoa (trẻ em).", result.Description);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static UpdateMissionActivityCommand BuildCommand(
        int activityId = 1, int? step = null, string? activityType = null) =>
        new(activityId, step, activityType, null, null, null, null, null, null);

    private static MissionActivityModel BuildActivity(
        int id = 1,
        MissionActivityStatus status = MissionActivityStatus.Planned,
        string activityType = "DELIVER_SUPPLIES") => new()
    {
        Id = id,
        MissionId = 10,
        Step = 1,
        ActivityType = activityType,
        Status = status,
        Description = "Original"
    };

    private static UpdateMissionActivityCommandHandler BuildHandler(
        StubActivityRepo? activityRepo = null,
        StubDepotRepo? depotRepo = null,
        StubAssemblyPointRepo? assemblyPointRepo = null,
        StubSosRequestRepo? sosRequestRepo = null,
        StubSosRequestUpdateRepo? sosRequestUpdateRepo = null)
    {
        return new UpdateMissionActivityCommandHandler(
            activityRepo ?? new StubActivityRepo(BuildActivity()),
            sosRequestRepo ?? new StubSosRequestRepo(),
            sosRequestUpdateRepo ?? new StubSosRequestUpdateRepo(),
            depotRepo ?? new StubDepotRepo(),
            assemblyPointRepo ?? new StubAssemblyPointRepo(null),
            new StubUnitOfWork(),
            NullLogger<UpdateMissionActivityCommandHandler>.Instance);
    }

    // ─── Stubs ────────────────────────────────────────────────────

    private sealed class StubActivityRepo(MissionActivityModel? activity) : IMissionActivityRepository
    {
        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(activity);
        public Task<int> AddAsync(MissionActivityModel a, CancellationToken ct = default) => Task.FromResult(a.Id);
        public Task UpdateAsync(MissionActivityModel a, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int mid, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MissionActivityModel>());
        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int apId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);
        public Task UpdateStatusAsync(int aid, MissionActivityStatus s, Guid db, string? img = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task AssignTeamAsync(int aid, int mtid, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> aids, Guid db, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSosRequestRepo(params SosRequestModel[] requests) : ISosRequestRepository
    {
        private readonly Dictionary<int, SosRequestModel> _requests = requests.ToDictionary(request => request.Id);

        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_requests.GetValueOrDefault(id));
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

    private sealed class StubSosRequestUpdateRepo : ISosRequestUpdateRepository
    {
        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(new Dictionary<int, SosRequestVictimUpdateModel>());
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubDepotRepo : IDepotInventoryRepository
    {
        public List<SupplyShortageResult> ShortagesToReturn { get; set; } = [];
        public int ReleaseCalls { get; private set; }
        public int CheckCalls { get; private set; }

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId, List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken ct = default)
        {
            CheckCalls++;
            return Task.FromResult(ShortagesToReturn);
        }
        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) => Task.FromResult(new MissionSupplyReservationResult());
        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken ct = default) { ReleaseCalls++; return Task.CompletedTask; }
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

    private sealed class StubAssemblyPointRepo(AssemblyPointModel? ap) : IAssemblyPointRepository
    {
        public Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(ap);
        public Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<AssemblyPointModel?>(null);
        public Task CreateAsync(AssemblyPointModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(AssemblyPointModel m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pn, int ps, CancellationToken ct = default, string? statusFilter = null) => Task.FromResult(new PagedResult<AssemblyPointModel>([], 0, pn, ps));
        public Task UnassignAllRescuersAsync(int assemblyPointId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<AssemblyPointModel>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<AssemblyPointModel>());
        public Task<Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>> GetTeamsByAssemblyPointIdsAsync(IEnumerable<int> ids, CancellationToken ct = default) => Task.FromResult(new Dictionary<int, List<RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById.AssemblyPointTeamDto>>());
        public Task<List<Guid>> GetAssignedRescuerUserIdsAsync(int apId, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> GetTeamlessRescuerUserIdsAsync(int apId, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<bool> HasActiveTeamAsync(Guid uid, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateRescuerAssemblyPointAsync(Guid uid, int? apId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<Guid>> BulkUpdateRescuerAssemblyPointAsync(IReadOnlyList<Guid> uids, int? apId, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> FilterUsersWithoutActiveTeamAsync(IReadOnlyList<Guid> uids, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
    }
}
