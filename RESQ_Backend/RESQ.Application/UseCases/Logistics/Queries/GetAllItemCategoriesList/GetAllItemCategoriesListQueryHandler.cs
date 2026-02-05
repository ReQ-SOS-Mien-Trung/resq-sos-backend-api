using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllItemCategoriesList;

public class GetAllItemCategoriesListQueryHandler(IItemCategoryRepository repository)
    : IRequestHandler<GetAllItemCategoriesListQuery, List<ItemCategoryDto>>
{
    private readonly IItemCategoryRepository _repository = repository;

    public async Task<List<ItemCategoryDto>> Handle(GetAllItemCategoriesListQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);

        return items.Select(c => new ItemCategoryDto
        {
            Id = c.Id,
            Code = c.Code.ToString(),
            Name = c.Name,
            Quantity = c.Quantity,
            Description = c.Description
        }).ToList();
    }
}