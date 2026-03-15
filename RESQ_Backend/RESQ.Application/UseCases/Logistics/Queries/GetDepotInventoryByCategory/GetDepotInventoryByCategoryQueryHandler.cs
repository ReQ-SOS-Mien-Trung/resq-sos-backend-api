using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

public class GetDepotInventoryByCategoryQueryHandler(
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetDepotInventoryByCategoryQuery, List<DepotCategoryQuantityDto>>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<List<DepotCategoryQuantityDto>> Handle(GetDepotInventoryByCategoryQuery request, CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
            throw new NotFoundException($"Không tìm thấy kho cứu trợ với ID: {request.DepotId}");

        return await _depotInventoryRepository.GetInventoryByCategoryAsync(request.DepotId, cancellationToken);
    }
}
