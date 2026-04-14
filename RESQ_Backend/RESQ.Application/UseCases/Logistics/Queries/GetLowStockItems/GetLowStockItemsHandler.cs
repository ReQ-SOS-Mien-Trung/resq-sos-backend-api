using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetLowStockItemsHandler(
    IDepotInventoryRepository depotInventoryRepo,
    IStockWarningEvaluatorService evaluatorService)
    : IRequestHandler<GetLowStockItemsQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly IStockWarningEvaluatorService _evaluatorService = evaluatorService;

    public async Task<LowStockChartResponseDto> Handle(
        GetLowStockItemsQuery request,
        CancellationToken cancellationToken)
    {
        var rawItems = await _depotInventoryRepo.GetLowStockRawItemsAsync(request.DepotId, cancellationToken);

        var items = new List<LowStockItemDto>();
        foreach (var raw in rawItems)
        {
            var result = await _evaluatorService.EvaluateAsync(
                raw.DepotId, raw.CategoryId, raw.ItemModelId, raw.AvailableQuantity, cancellationToken);

            // B? qua v?t ph?m dang OK
            if (result.Level == StockWarningLevel.Ok)
                continue;

            // B? qua UNCONFIGURED n?u không yêu c?u
            if (result.Level == StockWarningLevel.Unconfigured && !request.IncludeUnconfigured)
                continue;

            // L?c theo level n?u có
            if (request.WarningLevel != null &&
                !string.Equals(result.Level, request.WarningLevel, StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new LowStockItemDto
            {
                DepotId = raw.DepotId,
                DepotName = raw.DepotName,
                ItemModelId = raw.ItemModelId,
                ItemModelName = raw.ItemModelName,
                Unit = raw.Unit,
                CategoryId = raw.CategoryId,
                CategoryName = raw.CategoryName,
                TargetGroup = raw.TargetGroup,
                Quantity = raw.Quantity,
                ReservedQuantity = raw.ReservedQuantity,
                AvailableQuantity = raw.AvailableQuantity,
                MinimumThreshold = result.ResolvedThreshold,
                SeverityRatio = result.SeverityRatio,
                WarningLevel = result.Level,
                ResolvedThresholdScope = result.ResolvedScope.ToString(),
                IsUsingGlobalDefault = result.IsUsingGlobalDefault
            });
        }

        items = items
            .OrderBy(x => x.SeverityRatio)
            .ThenBy(x => x.DepotId)
            .ToList();

        return LowStockChartBuilder.Build(items);
    }
}

