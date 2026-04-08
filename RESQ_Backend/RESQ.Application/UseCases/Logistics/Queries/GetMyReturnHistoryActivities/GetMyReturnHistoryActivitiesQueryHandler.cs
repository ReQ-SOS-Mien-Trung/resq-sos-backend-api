using MediatR;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.Shared;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;

public class GetMyReturnHistoryActivitiesQueryHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IReturnSupplyActivityRepository returnSupplyActivityRepository)
    : IRequestHandler<GetMyReturnHistoryActivitiesQuery, PagedResult<ReturnHistoryActivityDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IReturnSupplyActivityRepository _returnSupplyActivityRepository = returnSupplyActivityRepository;

    public async Task<PagedResult<ReturnHistoryActivityDto>> Handle(
        GetMyReturnHistoryActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var paged = await _returnSupplyActivityRepository.GetHistoryPagedByDepotIdAsync(
            depotId,
            request.FromDate,
            request.ToDate,
            pageNumber,
            pageSize,
            cancellationToken);

        var items = paged.Items.Select(x => new ReturnHistoryActivityDto
        {
            DepotId = x.DepotId,
            DepotName = x.DepotName,
            DepotAddress = x.DepotAddress,
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
            CompletedAt = x.CompletedAt,
            CompletedBy = x.CompletedBy,
            CompletedByName = x.CompletedByName,
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

        return new PagedResult<ReturnHistoryActivityDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    private static ReturnSupplyActivityItemDto MapItem(ReturnSupplyActivityItemDetail item) => new()
    {
        ItemId = item.ItemId,
        ItemName = item.ItemName,
        Quantity = item.Quantity,
        Unit = item.Unit,
        ActualReturnedQuantity = item.ActualReturnedQuantity,
        ExpectedReturnUnits = item.ExpectedReturnUnits.Select(CloneUnit).ToList(),
        ReturnedReusableUnits = item.ReturnedReusableUnits.Select(CloneUnit).ToList()
    };

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