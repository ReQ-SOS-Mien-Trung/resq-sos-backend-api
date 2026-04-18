using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryById;

public record GetItemCategoryByIdQuery(int Id) : IRequest<ItemCategoryDto>;
