using MediatR;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyPickupHistoryActivities;

public class GetMyPickupHistoryActivitiesQueryHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IUpcomingPickupActivityRepository upcomingPickupActivityRepository)
    : IRequestHandler<GetMyPickupHistoryActivitiesQuery, PagedResult<PickupHistoryActivityDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IUpcomingPickupActivityRepository _upcomingPickupActivityRepository = upcomingPickupActivityRepository;

    public async Task<PagedResult<PickupHistoryActivityDto>> Handle(
        GetMyPickupHistoryActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var paged = await _upcomingPickupActivityRepository.GetHistoryPagedByDepotIdAsync(
            depotId,
            request.FromDate,
            request.ToDate,
            pageNumber,
            pageSize,
            cancellationToken);

        var items = paged.Items.Select(x => new PickupHistoryActivityDto
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
            Items = x.Items.Select(i => new PickupHistoryItemDto
            {
                ItemId = i.ItemId,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList()
        }).ToList();

        await ItemImageUrlEnricher.EnrichAsync(
            items.SelectMany(activity => activity.Items),
            item => item.ItemId,
            (item, imageUrl) => item.ImageUrl = imageUrl ?? item.ImageUrl,
            _itemModelMetadataRepository,
            cancellationToken);

        return new PagedResult<PickupHistoryActivityDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}