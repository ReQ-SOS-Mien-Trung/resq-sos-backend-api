using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

public class GetItemCategoriesQuery : IRequest<GetItemCategoriesResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
