using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventoryByCategory;

public class GetMyDepotInventoryByCategoryQueryHandler(IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<GetMyDepotInventoryByCategoryQuery, List<DepotCategoryQuantityDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<List<DepotCategoryQuantityDto>> Handle(GetMyDepotInventoryByCategoryQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken);
        if (!depotId.HasValue)
            throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        return await _depotInventoryRepository.GetInventoryByCategoryAsync(depotId.Value, cancellationToken);
    }
}
