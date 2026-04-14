using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions;

public class GetClosureTransferSuggestionsHandler : IRequestHandler<GetClosureTransferSuggestionsQuery, ClosureTransferSuggestionsResponse>
{
    private readonly IDepotRepository _depotRepository;

    public GetClosureTransferSuggestionsHandler(IDepotRepository depotRepository)
    {
        _depotRepository = depotRepository;
    }

    public async Task<ClosureTransferSuggestionsResponse> Handle(GetClosureTransferSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var sourceDepot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (sourceDepot == null)
            throw new NotFoundException($"Không tìm thấy kho có ID = {request.DepotId}");

        var inventoryItems = await _depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);

        var transferableItems = inventoryItems.Where(x => x.TransferableQuantity > 0).ToList();

        var availableDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();
        
        var targetDepots = availableDepots
            .Where(d => d.Id != request.DepotId)
            .Select(d => new TargetDepotMetricsDto
            {
                DepotId = d.Id,
                DepotName = d.Name,
                Capacity = d.Capacity,
                WeightCapacity = d.WeightCapacity,
                CurrentUtilization = d.CurrentUtilization,
                CurrentWeightUtilization = d.CurrentWeightUtilization,
                RemainingVolume = Math.Max(0, d.Capacity - d.CurrentUtilization),
                RemainingWeight = Math.Max(0, d.WeightCapacity - d.CurrentWeightUtilization)
            })
            // Only targets that have some room left
            .Where(d => d.RemainingVolume > 0 && d.RemainingWeight > 0)
            .ToList();

        var response = new ClosureTransferSuggestionsResponse
        {
            SourceDepotId = sourceDepot.Id,
            SourceDepotName = sourceDepot.Name,
            TargetDepotMetrics = targetDepots.OrderByDescending(x => x.RemainingVolume + x.RemainingWeight).ToList()
        };

        // Greedy allocation algorithm
        foreach (var item in transferableItems)
        {
            int remainingQty = item.TransferableQuantity;
            decimal volPerUnit = item.VolumePerUnit ?? 0.01m; // fallback to avoid div by 0
            decimal weightPerUnit = item.WeightPerUnit ?? 0.01m; // fallback

            decimal itemTotalVol = remainingQty * volPerUnit;
            decimal itemTotalWeight = remainingQty * weightPerUnit;

            response.TotalVolumeToTransfer += itemTotalVol;
            response.TotalWeightToTransfer += itemTotalWeight;

            // Sort targets by available capacity descending at each step to balance load
            var sortedTargets = targetDepots.OrderByDescending(t => t.RemainingVolume + t.RemainingWeight).ToList();

            foreach (var target in sortedTargets)
            {
                if (remainingQty <= 0) break;

                // How many units can fit in this target?
                int maxQtyByVol = (int)(target.RemainingVolume / volPerUnit);
                int maxQtyByWeight = (int)(target.RemainingWeight / weightPerUnit);

                int fitQty = Math.Min(remainingQty, Math.Min(maxQtyByVol, maxQtyByWeight));

                if (fitQty > 0)
                {
                    decimal volUsed = fitQty * volPerUnit;
                    decimal weightUsed = fitQty * weightPerUnit;

                    response.SuggestedTransfers.Add(new TransferSuggestionItemDto
                    {
                        TargetDepotId = target.DepotId,
                        TargetDepotName = target.DepotName,
                        ItemModelId = item.ItemModelId,
                        ItemName = item.ItemName,
                        ItemType = item.ItemType,
                        Unit = item.Unit,
                        SuggestedQuantity = fitQty,
                        TotalVolume = volUsed,
                        TotalWeight = weightUsed
                    });

                    target.RemainingVolume -= volUsed;
                    target.RemainingWeight -= weightUsed;
                    remainingQty -= fitQty;
                }
            }

            if (remainingQty > 0)
            {
                decimal unallocVol = remainingQty * volPerUnit;
                decimal unallocWeight = remainingQty * weightPerUnit;

                response.UnallocatedVolume += unallocVol;
                response.UnallocatedWeight += unallocWeight;

                response.SuggestedTransfers.Add(new TransferSuggestionItemDto
                {
                    TargetDepotId = null,
                    TargetDepotName = null,
                    ItemModelId = item.ItemModelId,
                    ItemName = item.ItemName,
                    ItemType = item.ItemType,
                    Unit = item.Unit,
                    SuggestedQuantity = remainingQty,
                    TotalVolume = unallocVol,
                    TotalWeight = unallocWeight
                });
            }
        }

        return response;
    }
}
