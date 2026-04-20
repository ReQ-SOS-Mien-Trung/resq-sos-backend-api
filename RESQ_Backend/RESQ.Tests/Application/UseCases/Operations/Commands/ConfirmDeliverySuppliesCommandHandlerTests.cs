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
using RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Logistics;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

public class ConfirmDeliverySuppliesCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeliversConsumableByLot_AndRefreshesReturnWithRemainingLotBalance()
    {
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int collectActivityId = 16;
        const int deliverActivityId = 17;
        const int returnActivityId = 18;
        const int riceItemId = 1;
        const int lotId = 501;
        var userId = Guid.NewGuid();

        var collectActivity = new MissionActivityModel
        {
            Id = collectActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 1,
            ActivityType = "COLLECT_SUPPLIES",
            Status = MissionActivityStatus.Succeed,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = riceItemId,
                    ItemName = "Gao",
                    Quantity = 10,
                    Unit = "kg",
                    PickupLotAllocations =
                    [
                        new SupplyExecutionLotDto
                        {
                            LotId = lotId,
                            QuantityTaken = 10,
                            ExpiredDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
                        }
                    ]
                }
            })
        };

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 2,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = riceItemId, ItemName = "Gao", Quantity = 8, Unit = "kg" }
            })
        };

        var returnActivity = new MissionActivityModel
        {
            Id = returnActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 3,
            ActivityType = "RETURN_SUPPLIES",
            Description = "Hoan tat nhiem vu, tra vat pham ve kho.",
            Status = MissionActivityStatus.Planned,
            Items = "[]"
        };

        var activityRepository = new StubMissionActivityRepository([collectActivity, deliverActivity, returnActivity]);
        var handler = CreateHandler(
            activityRepository,
            new Dictionary<int, ItemModelRecord>
            {
                [riceItemId] = new() { Id = riceItemId, Name = "Gao", Unit = "kg", ItemType = "Consumable" }
            });

        var response = await handler.Handle(new ConfirmDeliverySuppliesCommand(
            deliverActivityId,
            missionId,
            userId,
            [
                new ActualDeliveredItemDto
                {
                    ItemId = riceItemId,
                    ActualQuantity = 3,
                    LotAllocations =
                    [
                        new SupplyExecutionLotDto { LotId = lotId, QuantityTaken = 3 }
                    ]
                }
            ],
            null), CancellationToken.None);

        var deliveredItem = Assert.Single(response.DeliveredItems);
        var deliveredLot = Assert.Single(deliveredItem.DeliveredLotAllocations);
        var returnItem = Assert.Single(DeserializeSupplies(returnActivity.Items!));
        var expectedReturnLot = Assert.Single(returnItem.ExpectedReturnLotAllocations!);

        Assert.Equal(returnActivityId, response.SurplusReturnActivityId);
        Assert.Equal(lotId, deliveredLot.LotId);
        Assert.Equal(3, deliveredLot.QuantityTaken);
        Assert.Equal(riceItemId, returnItem.ItemId);
        Assert.Equal(7, returnItem.Quantity);
        Assert.Equal(lotId, expectedReturnLot.LotId);
        Assert.Equal(7, expectedReturnLot.QuantityTaken);
    }

    [Fact]
    public async Task Handle_RejectsLotDelivery_WhenQuantityExceedsCarriedLotBalance()
    {
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int collectActivityId = 16;
        const int deliverActivityId = 17;
        const int riceItemId = 1;
        const int lotId = 501;
        var userId = Guid.NewGuid();

        var collectActivity = new MissionActivityModel
        {
            Id = collectActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 1,
            ActivityType = "COLLECT_SUPPLIES",
            Status = MissionActivityStatus.Succeed,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = riceItemId,
                    ItemName = "Gao",
                    Quantity = 5,
                    Unit = "kg",
                    PickupLotAllocations =
                    [
                        new SupplyExecutionLotDto { LotId = lotId, QuantityTaken = 5 }
                    ]
                }
            })
        };

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 2,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = riceItemId, ItemName = "Gao", Quantity = 10, Unit = "kg" }
            })
        };

        var handler = CreateHandler(
            new StubMissionActivityRepository([collectActivity, deliverActivity]),
            new Dictionary<int, ItemModelRecord>
            {
                [riceItemId] = new() { Id = riceItemId, Name = "Gao", Unit = "kg", ItemType = "Consumable" }
            });

        var ex = await Assert.ThrowsAsync<RESQ.Application.Exceptions.BadRequestException>(() =>
            handler.Handle(new ConfirmDeliverySuppliesCommand(
                deliverActivityId,
                missionId,
                userId,
                [
                    new ActualDeliveredItemDto
                    {
                        ItemId = riceItemId,
                        ActualQuantity = 6,
                        LotAllocations =
                        [
                            new SupplyExecutionLotDto { LotId = lotId, QuantityTaken = 6 }
                        ]
                    }
                ],
                null), CancellationToken.None));

        Assert.Contains("vượt quá số đang mang theo", ex.Message);
    }

    [Fact]
    public async Task Handle_MergesConsumableSurplusIntoExistingReturnActivity()
    {
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int deliverActivityId = 17;
        const int returnActivityId = 18;
        const int riceItemId = 1;
        var userId = Guid.NewGuid();

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 6,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = riceItemId, ItemName = "Gao", Quantity = 360, Unit = "kg" }
            })
        };

        var returnActivity = new MissionActivityModel
        {
            Id = returnActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 7,
            ActivityType = "RETURN_SUPPLIES",
            Description = "Hoàn tất nhiệm vụ, trả vật phẩm về kho.",
            Status = MissionActivityStatus.Planned,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = 80,
                    ItemName = "Cang khieng thuong",
                    Quantity = 1,
                    Unit = "chiec",
                    ExpectedReturnUnits =
                    [
                        new SupplyExecutionReusableUnitDto
                        {
                            ReusableItemId = 279,
                            ItemModelId = 80,
                            ItemName = "Cang khieng thuong",
                            SerialNumber = "D3-R080-001"
                        }
                    ]
                }
            })
        };

        var activityRepository = new StubMissionActivityRepository([deliverActivity, returnActivity]);
        var handler = CreateHandler(
            activityRepository,
            new Dictionary<int, ItemModelRecord>
            {
                [riceItemId] = new() { Id = riceItemId, Name = "Gao", Unit = "kg", ItemType = "Consumable" }
            });

        var response = await handler.Handle(new ConfirmDeliverySuppliesCommand(
            deliverActivityId,
            missionId,
            userId,
            [new ActualDeliveredItemDto { ItemId = riceItemId, ActualQuantity = 350 }],
            "Giao thieu 10kg"), CancellationToken.None);

        var returnItems = DeserializeSupplies(returnActivity.Items!);
        var surplusItem = Assert.Single(returnItems, item => item.ItemId == riceItemId);

        Assert.Equal(returnActivityId, response.SurplusReturnActivityId);
        Assert.Equal(10, surplusItem.Quantity);
        Assert.Equal(2, activityRepository.Activities.Count);
        Assert.Contains("Bổ sung vật phẩm giao thiếu", returnActivity.Description);
    }

    [Fact]
    public async Task Handle_DoesNotCreateSeparateSurplusReturnForReusableShortfall()
    {
        const int missionId = 7;
        const int depotId = 3;
        const int missionTeamId = 6;
        const int deliverActivityId = 17;
        const int returnActivityId = 18;
        const int ropeItemId = 74;
        var userId = Guid.NewGuid();

        var expectedUnits = new List<SupplyExecutionReusableUnitDto>
        {
            new() { ReusableItemId = 267, ItemModelId = ropeItemId, ItemName = "Day thung cuu sinh 30m", SerialNumber = "D3-R074-001" },
            new() { ReusableItemId = 268, ItemModelId = ropeItemId, ItemName = "Day thung cuu sinh 30m", SerialNumber = "D3-R074-002" }
        };

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 6,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = ropeItemId, ItemName = "Day thung cuu sinh 30m", Quantity = 2, Unit = "cuon" }
            })
        };

        var returnActivity = new MissionActivityModel
        {
            Id = returnActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            DepotId = depotId,
            Step = 7,
            ActivityType = "RETURN_SUPPLIES",
            Description = "Hoàn tất nhiệm vụ, trả vật phẩm về kho.",
            Status = MissionActivityStatus.Planned,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new()
                {
                    ItemId = ropeItemId,
                    ItemName = "Day thung cuu sinh 30m",
                    Quantity = 2,
                    Unit = "cuon",
                    ExpectedReturnUnits = expectedUnits
                }
            })
        };

        var activityRepository = new StubMissionActivityRepository([deliverActivity, returnActivity]);
        var handler = CreateHandler(
            activityRepository,
            new Dictionary<int, ItemModelRecord>
            {
                [ropeItemId] = new() { Id = ropeItemId, Name = "Day thung cuu sinh 30m", Unit = "cuon", ItemType = "Reusable" }
            });

        var response = await handler.Handle(new ConfirmDeliverySuppliesCommand(
            deliverActivityId,
            missionId,
            userId,
            [new ActualDeliveredItemDto { ItemId = ropeItemId, ActualQuantity = 1 }],
            "Chi giao 1 cuon"), CancellationToken.None);

        var returnItem = Assert.Single(DeserializeSupplies(returnActivity.Items!));

        Assert.Null(response.SurplusReturnActivityId);
        Assert.Equal(1, Assert.Single(response.DeliveredItems).SurplusQuantity);
        Assert.Equal(2, activityRepository.Activities.Count);
        Assert.Equal(2, returnItem.Quantity);
        Assert.Equal(expectedUnits.Count, returnItem.ExpectedReturnUnits?.Count);
        Assert.DoesNotContain("Bổ sung vật phẩm giao thiếu", returnActivity.Description);
    }

    [Fact]
    public async Task Handle_SavesDeliveryNoteIntoMatchingDraftActivityReportSummary()
    {
        const int missionId = 7;
        const int missionTeamId = 6;
        const int deliverActivityId = 17;
        const int riceItemId = 1;
        var userId = Guid.NewGuid();

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            Step = 6,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = riceItemId, ItemName = "Gao", Quantity = 10, Unit = "kg" }
            })
        };

        var reportRepository = new StubMissionTeamReportRepository(new MissionTeamReportModel
        {
            MissionTeamId = missionTeamId,
            ReportStatus = MissionTeamReportStatus.Draft,
            ActivityReports =
            [
                new MissionActivityReportModel
                {
                    MissionActivityId = deliverActivityId,
                    ActivityType = "DELIVER_SUPPLIES",
                    Summary = "Đã tiếp cận người nhận"
                }
            ]
        });

        var handler = CreateHandler(
            new StubMissionActivityRepository([deliverActivity]),
            new Dictionary<int, ItemModelRecord>
            {
                [riceItemId] = new() { Id = riceItemId, Name = "Gao", Unit = "kg", ItemType = "Consumable" }
            },
            reportRepository);

        await handler.Handle(new ConfirmDeliverySuppliesCommand(
            deliverActivityId,
            missionId,
            userId,
            [new ActualDeliveredItemDto { ItemId = riceItemId, ActualQuantity = 8 }],
            "  Giao thiếu 2kg vì nước dâng nhanh  "), CancellationToken.None);

        Assert.Equal(1, reportRepository.UpsertDraftCallCount);

        var savedActivityReport = Assert.Single(reportRepository.CurrentReport!.ActivityReports);
        Assert.Equal("Đã tiếp cận người nhận" + Environment.NewLine + "Giao thiếu 2kg vì nước dâng nhanh", savedActivityReport.Summary);
        Assert.Equal(MissionActivityStatus.Succeed.ToString(), savedActivityReport.ExecutionStatus);
    }

    [Fact]
    public async Task Handle_DoesNotTouchDraftReportWhenDeliveryNoteIsMissing()
    {
        const int missionId = 7;
        const int missionTeamId = 6;
        const int deliverActivityId = 17;
        const int riceItemId = 1;
        var userId = Guid.NewGuid();

        var deliverActivity = new MissionActivityModel
        {
            Id = deliverActivityId,
            MissionId = missionId,
            MissionTeamId = missionTeamId,
            Step = 6,
            ActivityType = "DELIVER_SUPPLIES",
            Status = MissionActivityStatus.OnGoing,
            Items = JsonSerializer.Serialize(new List<SupplyToCollectDto>
            {
                new() { ItemId = riceItemId, ItemName = "Gao", Quantity = 10, Unit = "kg" }
            })
        };

        var reportRepository = new StubMissionTeamReportRepository();
        var handler = CreateHandler(
            new StubMissionActivityRepository([deliverActivity]),
            new Dictionary<int, ItemModelRecord>
            {
                [riceItemId] = new() { Id = riceItemId, Name = "Gao", Unit = "kg", ItemType = "Consumable" }
            },
            reportRepository);

        await handler.Handle(new ConfirmDeliverySuppliesCommand(
            deliverActivityId,
            missionId,
            userId,
            [new ActualDeliveredItemDto { ItemId = riceItemId, ActualQuantity = 10 }],
            null), CancellationToken.None);

        Assert.Equal(0, reportRepository.UpsertDraftCallCount);
        Assert.Null(reportRepository.CurrentReport);
    }

    private static ConfirmDeliverySuppliesCommandHandler CreateHandler(
        StubMissionActivityRepository activityRepository,
        Dictionary<int, ItemModelRecord> metadata,
        StubMissionTeamReportRepository? reportRepository = null)
    {
        var mediator = new RecordingMediator(request =>
        {
            if (request is UpdateActivityStatusCommand command)
            {
                activityRepository.SetStatus(command.ActivityId, command.Status);
                activityRepository.AutoStartNextReturnActivity(command.ActivityId);

                return new UpdateActivityStatusResponse
                {
                    ActivityId = command.ActivityId,
                    Status = command.Status.ToString(),
                    DecisionBy = command.DecisionBy
                };
            }

            return null;
        });

        return new ConfirmDeliverySuppliesCommandHandler(
            activityRepository,
            new StubItemModelMetadataRepository(metadata),
            reportRepository ?? new StubMissionTeamReportRepository(),
            mediator,
            new StubOperationalHubService(),
            new StubUnitOfWork(),
            NullLogger<ConfirmDeliverySuppliesCommandHandler>.Instance);
    }

    private static List<SupplyToCollectDto> DeserializeSupplies(string itemsJson) =>
        JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

    private sealed class StubMissionActivityRepository(List<MissionActivityModel> activities) : IMissionActivityRepository
    {
        private int _nextActivityId = 100;

        public List<MissionActivityModel> Activities { get; } = activities;

        public Task<MissionActivityModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Activities.FirstOrDefault(activity => activity.Id == id));

        public Task<IEnumerable<MissionActivityModel>> GetByMissionIdAsync(int missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<MissionActivityModel>>(Activities.Where(activity => activity.MissionId == missionId));

        public Task<IEnumerable<MissionActivityModel>> GetBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<MissionActivityModel>> GetOpenByAssemblyPointAsync(int assemblyPointId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MissionActivityModel>>(Activities.Where(activity => activity.AssemblyPointId == assemblyPointId).ToList());

        public Task<int> AddAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            activity.Id = _nextActivityId++;
            Activities.Add(activity);
            return Task.FromResult(activity.Id);
        }

        public Task UpdateAsync(MissionActivityModel activity, CancellationToken cancellationToken = default)
        {
            var existing = Activities.FirstOrDefault(item => item.Id == activity.Id);
            if (existing is not null && !ReferenceEquals(existing, activity))
            {
                existing.Items = activity.Items;
                existing.Description = activity.Description;
            }

            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(int activityId, MissionActivityStatus status, Guid decisionBy, string? imageUrl = null, CancellationToken cancellationToken = default)
        {
            SetStatus(activityId, status);
            return Task.CompletedTask;
        }

        public Task AssignTeamAsync(int activityId, int missionTeamId, CancellationToken cancellationToken = default)
        {
            var activity = Activities.FirstOrDefault(item => item.Id == activityId);
            if (activity is not null)
                activity.MissionTeamId = missionTeamId;

            return Task.CompletedTask;
        }

        public Task ResetAssignmentsToPlannedAsync(IEnumerable<int> activityIds, Guid decisionBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void SetStatus(int activityId, MissionActivityStatus status)
        {
            var activity = Activities.First(item => item.Id == activityId);
            activity.Status = status;
        }

        public void AutoStartNextReturnActivity(int completedActivityId)
        {
            var current = Activities.First(item => item.Id == completedActivityId);
            var nextReturn = Activities
                .Where(item => item.MissionId == current.MissionId
                    && item.MissionTeamId == current.MissionTeamId
                    && item.Status == MissionActivityStatus.Planned
                    && string.Equals(item.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                    && (item.Step ?? int.MaxValue) > (current.Step ?? int.MinValue))
                .OrderBy(item => item.Step ?? int.MaxValue)
                .ThenBy(item => item.Id)
                .FirstOrDefault();

            if (nextReturn is not null)
                nextReturn.Status = MissionActivityStatus.OnGoing;
        }
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

    private sealed class StubMissionTeamReportRepository(MissionTeamReportModel? report = null) : IMissionTeamReportRepository
    {
        public MissionTeamReportModel? CurrentReport { get; private set; } = report;
        public int UpsertDraftCallCount { get; private set; }

        public Task<MissionTeamReportModel?> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentReport?.MissionTeamId == missionTeamId ? CurrentReport : null);

        public Task<int> UpsertDraftAsync(MissionTeamReportModel model, CancellationToken cancellationToken = default)
        {
            UpsertDraftCallCount++;
            CurrentReport = model;
            return Task.FromResult(model.Id);
        }

        public Task SubmitAsync(int missionTeamId, Guid submittedBy, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateReportStatusAsync(int missionTeamId, MissionTeamReportStatus status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
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
}
