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
                    StockThresholdScopeType.Global        => "Toàn hệ thống",
                    StockThresholdScopeType.Depot         => "Theo kho",
                    StockThresholdScopeType.DepotCategory => "Theo danh mục trong kho",
                    StockThresholdScopeType.DepotItem     => "Theo vật phẩm trong kho",
                    _                                     => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
