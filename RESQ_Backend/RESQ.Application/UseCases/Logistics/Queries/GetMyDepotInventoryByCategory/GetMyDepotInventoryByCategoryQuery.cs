using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventoryByCategory;

public record GetMyDepotInventoryByCategoryQuery(Guid UserId) : IRequest<List<DepotCategoryQuantityDto>>;
