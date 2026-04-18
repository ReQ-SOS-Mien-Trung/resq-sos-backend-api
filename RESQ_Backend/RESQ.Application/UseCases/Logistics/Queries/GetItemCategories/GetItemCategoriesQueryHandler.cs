using MediatR;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

public class GetItemCategoriesQueryHandler(IItemCategoryRepository repository)
    : IRequestHandler<GetItemCategoriesQuery, GetItemCategoriesResponse>
{
    private readonly IItemCategoryRepository _repository = repository;

    public async Task<GetItemCategoriesResponse> Handle(GetItemCategoriesQuery request, CancellationToken cancellationToken)
    {
        // Use the new Paged method
        var pagedResult = await _repository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedResult.Items.Select(c => new ItemCategoryDto
        {
            Id = c.Id,
            Code = c.Code.ToString(),
            Name = c.Name,
            Quantity = c.Quantity,
            Description = c.Description
        }).ToList();

        return new GetItemCategoriesResponse
        {
            Items = dtos,
            PageNumber = pagedResult.PageNumber,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage
        };
    }
}
