using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;
using RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Application.UseCases.Logistics;

public class ReturnSupplyActivityQueryHandlerTests
{
    [Fact]
    public async Task UpcomingReturnsHandler_ThrowsNotFound_WhenUserHasNoActiveDepot()
    {
        var handler = new GetMyUpcomingReturnActivitiesQueryHandler(
            new StubDepotInventoryRepository { ActiveDepotId = null },
            new StubItemModelMetadataRepository(),
            new StubReturnSupplyActivityRepository());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetMyUpcomingReturnActivitiesQuery(Guid.NewGuid())
            {
                Status = MissionActivityStatus.OnGoing
            }, CancellationToken.None));

        Assert.Contains("không được chỉ định quản lý bất kỳ kho nào", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpcomingReturnsHandler_ForwardsSelectedStatusToRepository()
    {
        var repository = new StubReturnSupplyActivityRepository();
        var handler = new GetMyUpcomingReturnActivitiesQueryHandler(
            new StubDepotInventoryRepository { ActiveDepotId = 12 },
            new StubItemModelMetadataRepository(),
            repository);

        await handler.Handle(new GetMyUpcomingReturnActivitiesQuery(Guid.NewGuid())
        {
            Status = MissionActivityStatus.PendingConfirmation,
            PageNumber = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(MissionActivityStatus.PendingConfirmation, repository.LastRequestedUpcomingStatus);
    }

    [Fact]
    public async Task UpcomingReturnsHandler_UsesExpectedReusableUnitCount_WhenSnapshotExists()
    {
        const int depotId = 12;
        const int itemId = 80;

        var handler = new GetMyUpcomingReturnActivitiesQueryHandler(
            new StubDepotInventoryRepository { ActiveDepotId = depotId },
            new StubItemModelMetadataRepository(),
            new StubReturnSupplyActivityRepository
            {
                UpcomingResult = new PagedResult<UpcomingReturnActivityListItem>(
                    [
                        new UpcomingReturnActivityListItem
                        {
                            DepotId = depotId,
                            DepotName = "Kho test",
                            MissionId = 7,
                            ActivityId = 23,
                            ActivityType = "RETURN_SUPPLIES",
                            Status = MissionActivityStatus.PendingConfirmation.ToString(),
                            Items =
                            [
                                new ReturnSupplyActivityItemDetail
                                {
                                    ItemId = itemId,
                                    ItemName = "Cang khieng thuong",
                                    Quantity = 2,
                                    Unit = "chiec",
                                    ExpectedReturnUnits =
                                    [
                                        new SupplyExecutionReusableUnitDto { ReusableItemId = 171, ItemModelId = itemId, SerialNumber = "D2-R080-001" },
                                        new SupplyExecutionReusableUnitDto { ReusableItemId = 172, ItemModelId = itemId, SerialNumber = "D2-R080-002" },
                                        new SupplyExecutionReusableUnitDto { ReusableItemId = 173, ItemModelId = itemId, SerialNumber = "D2-R080-003" }
                                    ]
                                }
                            ]
                        }
                    ],
                    1,
                    1,
                    20)
            });

        var result = await handler.Handle(new GetMyUpcomingReturnActivitiesQuery(Guid.NewGuid())
        {
            Status = MissionActivityStatus.PendingConfirmation,
            PageNumber = 1,
            PageSize = 20
        }, CancellationToken.None);

        var activity = Assert.Single(result.Items);
        var item = Assert.Single(activity.Items);

        Assert.Equal(3, item.Quantity);
        Assert.Equal(3, item.ExpectedReturnUnits.Count);
    }

    [Fact]
    public async Task ReturnHistoryHandler_ThrowsNotFound_WhenUserHasNoActiveDepot()
    {
        var handler = new GetMyReturnHistoryActivitiesQueryHandler(
            new StubDepotInventoryRepository { ActiveDepotId = null },
            new StubItemModelMetadataRepository(),
            new StubReturnSupplyActivityRepository());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetMyReturnHistoryActivitiesQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("không được chỉ định quản lý bất kỳ kho nào", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnHistoryHandler_MapsSnapshotsAndEnrichesImageUrls()
    {
        const int depotId = 12;
        const int itemId = 4;
        const string imageUrl = "https://cdn.example/items/4.png";

        var handler = new GetMyReturnHistoryActivitiesQueryHandler(
            new StubDepotInventoryRepository { ActiveDepotId = depotId },
            new StubItemModelMetadataRepository(new Dictionary<int, ItemModelRecord>
            {
                [itemId] = new()
                {
                    Id = itemId,
                    Name = "Áo phao cứu sinh",
                    Unit = "chiếc",
                    ItemType = "Reusable",
                    ImageUrl = imageUrl
                }
            }),
            new StubReturnSupplyActivityRepository
            {
                HistoryResult = new PagedResult<ReturnHistoryActivityListItem>(
                    [
                        new ReturnHistoryActivityListItem
                        {
                            DepotId = depotId,
                            DepotName = "Kho Huế",
                            DepotAddress = "46 Đống Đa, Huế",
                            MissionId = 5,
                            MissionType = "Rescue",
                            MissionStatus = "OnGoing",
                            MissionStartTime = new DateTime(2026, 4, 9, 7, 0, 0, DateTimeKind.Utc),
                            MissionExpectedEndTime = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
                            ActivityId = 8,
                            Step = 4,
                            ActivityType = "RETURN_SUPPLIES",
                            Description = "Hoàn tất nhiệm vụ và trả đồ về kho.",
                            Priority = "Medium",
                            EstimatedTime = 30,
                            Status = "Succeed",
                            AssignedAt = new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Utc),
                            CompletedAt = new DateTime(2026, 4, 9, 10, 45, 0, DateTimeKind.Utc),
                            CompletedBy = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            CompletedByName = "Nguyen Van A",
                            MissionTeamId = 99,
                            RescueTeamId = 22,
                            RescueTeamName = "Team 22",
                            TeamType = "Boat",
                            Items =
                            [
                                new ReturnSupplyActivityItemDetail
                                {
                                    ItemId = itemId,
                                    ItemName = "Áo phao cứu sinh",
                                    Quantity = 2,
                                    Unit = "chiếc",
                                    ActualReturnedQuantity = 1,
                                    ExpectedReturnUnits =
                                    [
                                        new SupplyExecutionReusableUnitDto
                                        {
                                            ReusableItemId = 501,
                                            ItemModelId = itemId,
                                            ItemName = "Áo phao cứu sinh",
                                            SerialNumber = "D1-R004-001",
                                            Condition = "Good"
                                        }
                                    ],
                                    ReturnedReusableUnits =
                                    [
                                        new SupplyExecutionReusableUnitDto
                                        {
                                            ReusableItemId = 501,
                                            ItemModelId = itemId,
                                            ItemName = "Áo phao cứu sinh",
                                            SerialNumber = "D1-R004-001",
                                            Condition = "Used",
                                            Note = "Returned with note"
                                        }
                                    ]
                                }
                            ]
                        }
                    ],
                    1,
                    1,
                    20)
            });

        var result = await handler.Handle(new GetMyReturnHistoryActivitiesQuery(Guid.NewGuid())
        {
            PageNumber = 1,
            PageSize = 20
        }, CancellationToken.None);

        var activity = Assert.Single(result.Items);
        var item = Assert.Single(activity.Items);

        Assert.Equal("Kho Huế", activity.DepotName);
        Assert.Equal("Nguyen Van A", activity.CompletedByName);
        Assert.Equal(imageUrl, item.ImageUrl);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(1, item.ActualReturnedQuantity);
        Assert.Single(item.ExpectedReturnUnits);
        Assert.Equal("D1-R004-001", item.ExpectedReturnUnits[0].SerialNumber);
        Assert.Single(item.ReturnedReusableUnits);
        Assert.Equal("Returned with note", item.ReturnedReusableUnits[0].Note);
    }

    private sealed class StubReturnSupplyActivityRepository : IReturnSupplyActivityRepository
    {
        public MissionActivityStatus? LastRequestedUpcomingStatus { get; private set; }

        public PagedResult<UpcomingReturnActivityListItem> UpcomingResult { get; set; }
            = new([], 0, 1, 20);

        public PagedResult<ReturnHistoryActivityListItem> HistoryResult { get; set; }
            = new([], 0, 1, 20);

        public Task<PagedResult<UpcomingReturnActivityListItem>> GetPagedByDepotIdAsync(
            int depotId,
            MissionActivityStatus status,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastRequestedUpcomingStatus = status;
            return Task.FromResult(UpcomingResult);
        }

        public Task<PagedResult<ReturnHistoryActivityListItem>> GetHistoryPagedByDepotIdAsync(
            int depotId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HistoryResult);
    }

    private sealed class StubItemModelMetadataRepository : IItemModelMetadataRepository
    {
        private readonly Dictionary<int, ItemModelRecord> _records;

        public StubItemModelMetadataRepository(Dictionary<int, ItemModelRecord>? records = null)
        {
            _records = records ?? [];
        }

        public Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
        {
            var result = ids
                .Distinct()
                .Where(_records.ContainsKey)
                .ToDictionary(id => id, id => _records[id]);

            return Task.FromResult(result);
        }

        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public int? ActiveDepotId { get; set; }

        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveDepotId);

        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveDepotId.HasValue ? new List<int> { ActiveDepotId.Value } : []);

        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes,
            List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword, int page,
            int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds,
            Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId,
            List<(int ItemModelId, string ItemName, int RequestedQuantity)> items,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items,
            Guid performedBy, int activityId, int missionId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int depotId, int missionId, int activityId, Guid performedBy,
            List<(int ItemModelId, int Quantity)> consumableItems,
            List<(int ReusableItemId, string? Condition, string? Note)> reusableItems,
            List<(int ItemModelId, int Quantity)> legacyReusableQuantities,
            string? discrepancyNote,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExportInventoryAsync(int depotId, int itemModelId, int quantity, Guid performedBy, string? note,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AdjustInventoryAsync(int depotId, int itemModelId, int quantityChange, Guid performedBy, string reason, string? note,
            DateTime? expiredDate, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(int sourceDepotId, int targetDepotId,
            int closureId, Guid performedBy, int? lastProcessedInventoryId = null, int batchSize = 100,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
