using MediatR;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.Shared;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;

public class GetMyUpcomingReturnActivitiesQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IReturnSupplyActivityRepository returnSupplyActivityRepository)
    : IRequestHandler<GetMyUpcomingReturnActivitiesQuery, PagedResult<UpcomingReturnActivityDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IReturnSupplyActivityRepository _returnSupplyActivityRepository = returnSupplyActivityRepository;

    public async Task<PagedResult<UpcomingReturnActivityDto>> Handle(
        GetMyUpcomingReturnActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var paged = await _returnSupplyActivityRepository.GetPagedByDepotIdAsync(
            depotId,
            request.Status,
            pageNumber,
            pageSize,
            cancellationToken);

        var items = paged.Items.Select(x => new UpcomingReturnActivityDto
        {
            DepotId = x.DepotId,
            DepotName = x.DepotName,
            MissionId = x.MissionId,
            MissionType = x.MissionType,
            MissionStatus = x.MissionStatus,
            MissionStartTime = x.MissionStartTime,
            MissionExpectedEndTime = x.MissionExpectedEndTime,
            ActivityId = x.ActivityId,
            Step = x.Step,
            ActivityType = x.ActivityType,
            Description = x.Description,
            Priority = x.Priority,
            EstimatedTime = x.EstimatedTime,
            Status = x.Status,
            AssignedAt = x.AssignedAt,
            MissionTeamId = x.MissionTeamId,
            RescueTeamId = x.RescueTeamId,
            RescueTeamName = x.RescueTeamName,
            TeamType = x.TeamType,
            Items = x.Items.Select(MapItem).ToList()
        }).ToList();

        await ItemImageUrlEnricher.EnrichAsync(
            items.SelectMany(activity => activity.Items),
            item => item.ItemId,
            (item, imageUrl) => item.ImageUrl = imageUrl ?? item.ImageUrl,
            _itemModelMetadataRepository,
            cancellationToken);

        return new PagedResult<UpcomingReturnActivityDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    private static ReturnSupplyActivityItemDto MapItem(ReturnSupplyActivityItemDetail item)
    {
        var expectedReturnLots = item.ExpectedReturnLotAllocations.Select(CloneLot).ToList();
        var expectedReturnUnits = item.ExpectedReturnUnits.Select(CloneUnit).ToList();
        var returnedLots = item.ReturnedLotAllocations.Select(CloneLot).ToList();
        var returnedUnits = item.ReturnedReusableUnits.Select(CloneUnit).ToList();

        return new ReturnSupplyActivityItemDto
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            Quantity = ResolveDisplayQuantity(item, expectedReturnLots, expectedReturnUnits, returnedLots, returnedUnits),
            Unit = item.Unit,
            ActualReturnedQuantity = item.ActualReturnedQuantity,
            ExpectedReturnLotAllocations = expectedReturnLots,
            ReturnedLotAllocations = returnedLots,
            ExpectedReturnUnits = expectedReturnUnits,
            ReturnedReusableUnits = returnedUnits,
            PickupLotAllocations = item.PickupLotAllocations.Select(CloneLot).ToList()
        };
    }

    private static int ResolveDisplayQuantity(
        ReturnSupplyActivityItemDetail item,
        IReadOnlyCollection<SupplyExecutionLotDto> expectedReturnLots,
        IReadOnlyCollection<SupplyExecutionReusableUnitDto> expectedReturnUnits,
        IReadOnlyCollection<SupplyExecutionLotDto> returnedLots,
        IReadOnlyCollection<SupplyExecutionReusableUnitDto> returnedUnits)
    {
        if (expectedReturnUnits.Count > 0)
            return expectedReturnUnits.Count;

        var expectedLotQuantity = expectedReturnLots.Sum(lot => lot.QuantityTaken);
        if (expectedLotQuantity > 0)
            return expectedLotQuantity;

        if (returnedUnits.Count > 0)
            return returnedUnits.Count;

        var returnedLotQuantity = returnedLots.Sum(lot => lot.QuantityTaken);
        if (returnedLotQuantity > 0)
            return returnedLotQuantity;

        if (item.ActualReturnedQuantity.GetValueOrDefault() > 0)
            return item.ActualReturnedQuantity!.Value;

        return item.Quantity;
    }

    private static SupplyExecutionReusableUnitDto CloneUnit(SupplyExecutionReusableUnitDto unit) => new()
    {
        ReusableItemId = unit.ReusableItemId,
        ItemModelId = unit.ItemModelId,
        ItemName = unit.ItemName,
        SerialNumber = unit.SerialNumber,
        Condition = unit.Condition,
        Note = unit.Note
    };

    private static SupplyExecutionLotDto CloneLot(SupplyExecutionLotDto lot) => new()
    {
        LotId = lot.LotId,
        QuantityTaken = lot.QuantityTaken,
        ReceivedDate = lot.ReceivedDate,
        ExpiredDate = lot.ExpiredDate,
        RemainingQuantityAfterExecution = lot.RemainingQuantityAfterExecution
    };
}
