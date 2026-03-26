using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholdHistory;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAdminThresholdHistory;

public class GetAdminThresholdHistoryHandler(
    IStockThresholdConfigRepository stockThresholdConfigRepository)
    : IRequestHandler<GetAdminThresholdHistoryQuery, PagedResult<ThresholdHistoryItemDto>>
{
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;

    public async Task<PagedResult<ThresholdHistoryItemDto>> Handle(GetAdminThresholdHistoryQuery request, CancellationToken cancellationToken)
    {
        var paged = await _stockThresholdConfigRepository.GetAdminHistoryPagedAsync(
            request.DepotId,
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
