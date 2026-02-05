using MediatR;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryByCode;

public record GetItemCategoryByCodeQuery(ItemCategoryCode Code) : IRequest<ItemCategoryDto>;