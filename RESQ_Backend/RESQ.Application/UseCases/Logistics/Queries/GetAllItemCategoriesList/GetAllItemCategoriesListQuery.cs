using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllItemCategoriesList;

public record GetAllItemCategoriesListQuery : IRequest<List<ItemCategoryDto>>;