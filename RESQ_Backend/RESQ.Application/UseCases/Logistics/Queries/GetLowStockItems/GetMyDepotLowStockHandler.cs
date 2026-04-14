using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class GetMyDepotLowStockHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepo,
    IStockWarningEvaluatorService evaluatorService)
    : IRequestHandler<GetMyDepotLowStockQuery, LowStockChartResponseDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IStockWarningEvaluatorService _evaluatorService = evaluatorService;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<LowStockChartResponseDto> Handle(
        GetMyDepotLowStockQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("T�i kho?n hi?n t?i kh�ng du?c ch? d?nh qu?n l� b?t k? kho n�o dang ho?t d?ng.");

        var rawItems = await _depotInventoryRepo.GetLowStockRawItemsAsync(depotId, cancellationToken);
        var items = new List<LowStockItemDto>();

        foreach (var raw in rawItems)
        {
            var result = await _evaluatorService.EvaluateAsync(
                raw.DepotId, raw.CategoryId, raw.ItemModelId, raw.AvailableQuantity, cancellationToken);

            // B? qua v?t ph?m dang OK
            if (result.Level == StockWarningLevel.Ok)
                continue;

            // B? qua UNCONFIGURED n?u kh�ng y�u c?u
            if (result.Level == StockWarningLevel.Unconfigured && !request.IncludeUnconfigured)
                continue;

            // L?c theo level n?u c�
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

