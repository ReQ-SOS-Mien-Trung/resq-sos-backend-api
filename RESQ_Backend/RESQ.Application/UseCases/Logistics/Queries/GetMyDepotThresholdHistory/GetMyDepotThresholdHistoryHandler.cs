using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholdHistory;

public class GetMyDepotThresholdHistoryHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IStockThresholdConfigRepository stockThresholdConfigRepository)
    : IRequestHandler<GetMyDepotThresholdHistoryQuery, PagedResult<ThresholdHistoryItemDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;

    public async Task<PagedResult<ThresholdHistoryItemDto>> Handle(GetMyDepotThresholdHistoryQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var paged = await _stockThresholdConfigRepository.GetHistoryPagedAsync(
            depotId,
            request.ScopeType,
            request.CategoryId,
            request.ItemModelId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = paged.Items.Select(x => new ThresholdHistoryItemDto
        {
            Id = x.Id,
            ConfigId = x.ConfigId,
            ScopeType = x.ScopeType.ToString(),
            DepotId = x.DepotId,
            CategoryId = x.CategoryId,
            ItemModelId = x.ItemModelId,
            OldDangerPercent = x.OldDangerRatio.HasValue ? x.OldDangerRatio.Value * 100m : null,
            OldWarningPercent = x.OldWarningRatio.HasValue ? x.OldWarningRatio.Value * 100m : null,
            NewDangerPercent = x.NewDangerRatio.HasValue ? x.NewDangerRatio.Value * 100m : null,
            NewWarningPercent = x.NewWarningRatio.HasValue ? x.NewWarningRatio.Value * 100m : null,
            ChangedBy = x.ChangedBy,
            ChangedAt = x.ChangedAt.ToVietnamTime(),
            ChangeReason = x.ChangeReason,
            Action = x.Action
        }).ToList();

        return new PagedResult<ThresholdHistoryItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
