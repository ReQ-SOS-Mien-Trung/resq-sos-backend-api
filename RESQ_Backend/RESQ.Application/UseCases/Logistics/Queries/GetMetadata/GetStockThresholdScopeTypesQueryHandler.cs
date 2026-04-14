using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetStockThresholdScopeTypesQueryHandler : IRequestHandler<GetStockThresholdScopeTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetStockThresholdScopeTypesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<StockThresholdScopeType>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    StockThresholdScopeType.Global        => "Toàn h? th?ng",
                    StockThresholdScopeType.Depot         => "Theo kho",
                    StockThresholdScopeType.DepotCategory => "Theo danh m?c trong kho",
                    StockThresholdScopeType.DepotItem     => "Theo v?t ph?m trong kho",
                    _                                     => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
