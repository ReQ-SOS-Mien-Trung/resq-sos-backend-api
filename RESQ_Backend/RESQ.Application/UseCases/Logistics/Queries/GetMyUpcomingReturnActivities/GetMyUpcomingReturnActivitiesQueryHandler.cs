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
        var expectedReturnUnits = item.ExpectedReturnUnits.Select(CloneUnit).ToList();

        return new ReturnSupplyActivityItemDto
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            Quantity = expectedReturnUnits.Count > 0 ? expectedReturnUnits.Count : item.Quantity,
            Unit = item.Unit,
            ActualReturnedQuantity = item.ActualReturnedQuantity,
            ExpectedReturnUnits = expectedReturnUnits,
            ReturnedReusableUnits = item.ReturnedReusableUnits.Select(CloneUnit).ToList(),
            PickupLotAllocations = item.PickupLotAllocations
        };
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
}
