using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryCodes;

public record GetItemCategoryCodesQuery : IRequest<List<ItemCategoryCodeDto>>;