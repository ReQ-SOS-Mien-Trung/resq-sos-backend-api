using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

public record GetDepotInventoryByCategoryQuery(int DepotId) : IRequest<List<DepotCategoryQuantityDto>>;
