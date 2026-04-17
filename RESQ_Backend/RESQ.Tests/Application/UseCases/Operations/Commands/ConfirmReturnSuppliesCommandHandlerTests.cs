using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class ConfirmReturnSuppliesCommandHandlerTests
{
    [Fact]
    public async Task Handle_UsesExpectedReusableUnitCount_WhenQuantitySnapshotIsStale()
    {
        const int activityId = 23;
        const int missionId = 7;
        const int depotId = 2;
        const int itemId = 80;
        var userId = Guid.NewGuid();

        var expectedUnits = new List<SupplyExecutionReusableUnitDto>
        {
            new() { ReusableItemId = 171, ItemModelId = itemId, ItemName = "Cang khieng thuong", SerialNumber = "D2-R080-001" },
            new() { ReusableItemId = 172, ItemModelId = itemId, ItemName = "Cang khieng thuong", SerialNumber = "D2-R080-002" },
            new() { ReusableItemId = 173, ItemModelId = itemId, ItemName = "Cang khieng thuong", SerialNumber = "D2-R080-003" }
        };

        var activity = new MissionActivityModel
        {
            Id = activityId,
            MissionId = missionId,
            DepotId = depotId,
            ActivityType = "RETURN_SUPPLIES",
            Status = MissionActivityStatus.PendingConfirmation,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = itemId,
                    ItemName = "Cang khieng thuong",
                    Quantity = 2,
                    Unit = "chiec",
                    ExpectedReturnUnits = expectedUnits
                }
            })
        };

        var activityRepository = new StubMissionActivityRepository(activity);
        var depotInventoryRepository = new StubDepotInventoryRepository
        {
            ManagerDepotIds = [depotId],
            ReturnResult = new MissionSupplyReturnExecutionResult
            {
                Items =
                [
                    new MissionSupplyReturnExecutionItemDto
                    {
                        ItemModelId = itemId,
                        ItemName = "Cang khieng thuong",
                        Unit = "chiec",
                        ActualQuantity = 3,
                        ReturnedReusableUnits = expectedUnits
                    }
                ]
            }
        };
        var metadataRepository = new StubItemModelMetadataRepository(new Dictionary<int, ItemModelRecord>
        {
            [itemId] = new() { Id = itemId, Name = "Cang khieng thuong", Unit = "chiec", ItemType = "Reusable" }
        });
        var handler = new ConfirmReturnSuppliesCommandHandler(
            activityRepository,
            depotInventoryRepository,
            metadataRepository,
            
            new DummyMediator(), new StubOperationalHubService(), new StubUnitOfWork(), NullLogger<ConfirmReturnSuppliesCommandHandler>.Instance);

        var response = await handler.Handle(new ConfirmReturnSuppliesCommand(
            activityId,
            missionId,
            userId,
            [],
            [
                new ActualReturnedReusableItemDto
                {
                    ItemModelId = itemId,
                    Quantity = 2,
                    Units = expectedUnits
                        .Select(unit => new ActualReturnedReusableUnitDto { ReusableItemId = unit.ReusableItemId })
                        .ToList()
                }
            ],
            null), CancellationToken.None);

        var restoredItem = Assert.Single(response.RestoredItems);

        Assert.False(response.DiscrepancyRecorded);
        Assert.Equal(3, restoredItem.ExpectedQuantity);
        Assert.Equal(3, depotInventoryRepository.ReceivedReusableItems.Count);
    }

    [Fact]
    public async Task Handle_RebuildsExpectedReusableUnitsFromPickedCollectUnitsBeforeValidation()
    {
        const int collectActivityId = 12;
        const int returnActivityId = 19;
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int itemId = 74;
        var userId = Guid.NewGuid();

        var pickedUnit = new SupplyExecutionReusableUnitDto
        {
            ReusableItemId = 267,
            ItemModelId = itemId,
            ItemName = "Day thung cuu sinh 30m",
            SerialNumber = "D3-R074-001"
        };
        var unusedBufferUnit = new SupplyExecutionReusableUnitDto
        {
            ReusableItemId = 268,
            ItemModelId = itemId,
            ItemName = "Day thung cuu sinh 30m",
            SerialNumber = "D3-R074-002"
        };

        var collectActivity = new MissionActivityModel
        {
            Id = collectActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "COLLECT_SUPPLIES",
            Status = MissionActivityStatus.Succeed,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = itemId,
                    ItemName = "Day thung cuu sinh 30m",
                    Quantity = 1,
                    Unit = "cuon",
                    PlannedPickupReusableUnits = [pickedUnit, unusedBufferUnit],
                    PickedReusableUnits = [pickedUnit]
                }
            })
        };

        var returnActivity = new MissionActivityModel
        {
            Id = returnActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            ActivityType = "RETURN_SUPPLIES",
            Status = MissionActivityStatus.PendingConfirmation,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = itemId,
                    ItemName = "Day thung cuu sinh 30m",
                    Quantity = 1,
                    Unit = "cuon",
                    ExpectedReturnUnits = [pickedUnit, unusedBufferUnit]
                }
            })
        };

        var activityRepository = new StubMissionActivityRepository([collectActivity, returnActivity]);
        var depotInventoryRepository = new StubDepotInventoryRepository
        {
            ManagerDepotIds = [depotId],
            ReturnResult = new MissionSupplyReturnExecutionResult
            {
                Items =
                [
                    new MissionSupplyReturnExecutionItemDto
                    {
                        ItemModelId = itemId,
                        ItemName = "Day thung cuu sinh 30m",
                        Unit = "cuon",
                        ActualQuantity = 1,
                        ReturnedReusableUnits = [pickedUnit]
                    }
                ]
            }
        };
        var metadataRepository = new StubItemModelMetadataRepository(new Dictionary<int, ItemModelRecord>
        {
            [itemId] = new() { Id = itemId, Name = "Day thung cuu sinh 30m", Unit = "cuon", ItemType = "Reusable" }
        });
        var handler = new ConfirmReturnSuppliesCommandHandler(
            activityRepository,
            depotInventoryRepository,
            metadataRepository,
            
            new DummyMediator(), new StubOperationalHubService(), new StubUnitOfWork(), NullLogger<ConfirmReturnSuppliesCommandHandler>.Instance);

        var response = await handler.Handle(new ConfirmReturnSuppliesCommand(
            returnActivityId,
            missionId,
            userId,
            [],
            [
                new ActualReturnedReusableItemDto
                {
                    ItemModelId = itemId,
                    Units =
                    [
                        new ActualReturnedReusableUnitDto { ReusableItemId = pickedUnit.ReusableItemId }
                    ]
                }
            ],
            null), CancellationToken.None);

        var restoredItem = Assert.Single(response.RestoredItems);
        var returnItem = Assert.Single(JsonSerializer.Deserialize<List<SupplyToCollectDto>>(returnActivity.Items!) ?? []);
        var expectedUnit = Assert.Single(returnItem.ExpectedReturnUnits!);

        Assert.False(response.DiscrepancyRecorded);
        Assert.Equal(1, restoredItem.ExpectedQuantity);
        Assert.Equal(pickedUnit.ReusableItemId, expectedUnit.ReusableItemId);
        Assert.DoesNotContain(depotInventoryRepository.ReceivedReusableItems, item => item.ReusableItemId == unusedBufferUnit.ReusableItemId);
    }

    [Fact]
    public async Task Handle_MapsInventoryInvalidOperation_ToBadRequest()
    {
        const int activityId = 24;
        const int missionId = 7;
        const int depotId = 2;
        const int itemId = 80;
        var userId = Guid.NewGuid();

        var activity = new MissionActivityModel
        {
            Id = activityId,
            MissionId = missionId,
            DepotId = depotId,
            ActivityType = "RETURN_SUPPLIES",
            Status = MissionActivityStatus.PendingConfirmation,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = itemId,
                    ItemName = "Cang khieng thuong",
                    Quantity = 1,
                    Unit = "chiec",
                    ExpectedReturnUnits =
                    [
                        new SupplyExecutionReusableUnitDto
                        {
                            ReusableItemId = 171,
                            ItemModelId = itemId,
                            ItemName = "Cang khieng thuong",
                            SerialNumber = "D2-R080-001"
                        }
                    ]
                }
            })
        };

        var activityRepository = new StubMissionActivityRepository(activity);
        var depotInventoryRepository = new StubDepotInventoryRepository
        {
            ManagerDepotIds = [depotId],
            ExceptionFactory = () => new InvalidOperationException("Reusable unit #171 kh�ng ? tr?ng th�i InUse.")
        };
        var metadataRepository = new StubItemModelMetadataRepository(new Dictionary<int, ItemModelRecord>
        {
            [itemId] = new() { Id = itemId, Name = "Cang khieng thuong", Unit = "chiec", ItemType = "Reusable" }
        });
        var handler = new ConfirmReturnSuppliesCommandHandler(
            activityRepository,
            depotInventoryRepository,
            metadataRepository,
            
            new DummyMediator(), new StubOperationalHubService(), new StubUnitOfWork(), NullLogger<ConfirmReturnSuppliesCommandHandler>.Instance);

        var ex = await Assert.ThrowsAsync<RESQ.Application.Exceptions.BadRequestException>(() =>
            handler.Handle(new ConfirmReturnSuppliesCommand(
                activityId,
                missionId,
                userId,
                [],
                [
                    new ActualReturnedReusableItemDto
                    {
                        ItemModelId = itemId,
                        Units =
                        [
                            new ActualReturnedReusableUnitDto { ReusableItemId = 171 }
                        ]
                    }
                ],
                null), CancellationToken.None));

        Assert.Equal("Reusable unit #171 kh�ng ? tr?ng th�i InUse.", ex.Message);
    }

    [Fact]
    public async Task Handle_AllowsNullNestedCollections_AndReturnsBusinessErrorInsteadOf500()
    {
        const int activityId = 25;
        const int missionId = 7;
        const int depotId = 2;
        const int itemId = 80;
        var userId = Guid.NewGuid();

        var activity = new MissionActivityModel
        {
            Id = activityId,
            MissionId = missionId,
            DepotId = depotId,
            ActivityType = "RETURN_SUPPLIES",
            Status = MissionActivityStatus.PendingConfirmation,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = itemId,
                    ItemName = "Cang khieng thuong",
                    Quantity = 1,
                    Unit = "chiec"
                }
            })
        };

        var handler = new ConfirmReturnSuppliesCommandHandler(
            new StubMissionActivityRepository(activity),
            new StubDepotInventoryRepository
            {
                ManagerDepotIds = [depotId]
            },
            new StubItemModelMetadataRepository(new Dictionary<int, ItemModelRecord>
            {
                [itemId] = new() { Id = itemId, Name = "Cang khieng thuong", Unit = "chiec", ItemType = "Reusable" }
            }),
            
            new DummyMediator(), new StubOperationalHubService(), new StubUnitOfWork(), NullLogger<ConfirmReturnSuppliesCommandHandler>.Instance);

        var ex = await Assert.ThrowsAsync<RESQ.Application.Exceptions.BadRequestException>(() =>
            handler.Handle(new ConfirmReturnSuppliesCommand(
                activityId,
                missionId,
                userId,
                null!,
                [
                    new ActualReturnedReusableItemDto
                    {
                        ItemModelId = itemId,
                        Quantity = null,
                        Units = null!
                    }
                ],
                null), CancellationToken.None));

        Assert.Contains("phai nhap ly do chenh lech", RemoveDiacritics(ex.Message), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubMissionActivityRepository : IMissionActivityRepository
    {
        private readonly List<MissionActivityModel> _activities;

        public StubMissionActivityRepository(MissionActivityModel activity)
            : this([activity])
        {
        }

        public StubMissionActivityRepository(IEnumerable<MissionActivityModel> activities)
        {
            _activities = activities.ToList();
        }

        public MissionActivityStatus? UpdatedStatus { get; private set; }

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_activities.FirstOrDefault(activity => activity.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionActivityModel>>(_activities.Where(activity => activity.MissionId == missionId));

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MissionActivityModel>>([]);

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(MissionActivityModel updatedActivity, CancellationToken cancellationToken = default)
        {
            var activity = _activities.FirstOrDefault(item => item.Id == updatedActivity.Id);
            if (activity is not null)
            {
                activity.Items = updatedActivity.Items;
                activity.Description = updatedActivity.Description;
            }

            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
        {
            UpdatedStatus = status;
            var activity = _activities.FirstOrDefault(item => item.Id == activityId);
            if (activity is not null)
                activity.Status = status;

            return Task.CompletedTask;
        }

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubItemModelMetadataRepository(Dictionary<int, ItemModelRecord> records) : IItemModelMetadataRepository
    {
        public Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DonationImportTargetGroupInfo>> GetAllTargetGroupsForTemplateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
            => Task.FromResult(ids.Where(records.ContainsKey).ToDictionary(id => id, id => records[id]));

        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public List<int> ManagerDepotIds { get; set; } = [];
        public MissionSupplyReturnExecutionResult ReturnResult { get; set; } = new();
        public List<(int ReusableItemId, string? Condition, string? Note)> ReceivedReusableItems { get; private set; } = [];
        public Func<Exception>? ExceptionFactory { get; set; }

        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ManagerDepotIds);

        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int depotId, List<int>? categoryIds, List<ItemType>? itemTypes,
            List<TargetGroup>? targetGroups, string? itemName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int depotId, int itemModelId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string categoryKeyword, string? typeKeyword,
            int page, int pageSize, IReadOnlyCollection<int>? allowedDepotIds = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? itemModelIds,
            Dictionary<int, int> itemQuantities, bool activeDepotsOnly, int? excludeDepotId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int depotId,
            List<(int ItemModelId, string ItemName, int RequestedQuantity)> items, CancellationToken cancellationToken = default)
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
        {
            if (ExceptionFactory is not null)
                throw ExceptionFactory();

            ReceivedReusableItems = reusableItems;
            return Task.FromResult(ReturnResult);
        }

        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReleaseReservedSuppliesAsync(int depotId, List<(int ItemModelId, int Quantity)> items, CancellationToken cancellationToken = default)
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

        public Task TransferClosureItemsAsync(int sourceDepotId, int targetDepotId, int closureId, int transferId,
            Guid performedBy, IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task ZeroOutForClosureAsync(int depotId, int closureId, Guid performedBy, string? note, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task DisposeConsumableLotAsync(int lotId, int quantity, string reason, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DecommissionReusableItemAsync(int reusableItemId, string? note, Guid performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ExpiringLotModel>> GetExpiringLotsAsync(int depotId, int daysAhead, CancellationToken cancellationToken = default) => Task.FromResult(new List<ExpiringLotModel>());
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => throw new NotImplementedException();
        public Task<int> SaveChangesWithTransactionAsync() => throw new NotImplementedException();
        public Task<int> SaveAsync() => Task.FromResult(1);
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class { }
        public Task ExecuteInTransactionAsync(Func<Task> action) => action();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder
            .ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace('d', 'd')
            .Replace('�', 'D');
    }
}








    public class DummyMediator : MediatR.IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(TResponse)!);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }



