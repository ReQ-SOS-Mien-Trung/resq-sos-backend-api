using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryById;

public class GetItemCategoryByIdQueryHandler(IItemCategoryRepository repository)
    : IRequestHandler<GetItemCategoryByIdQuery, ItemCategoryDto>
{
    private readonly IItemCategoryRepository _repository = repository;

    public async Task<ItemCategoryDto> Handle(GetItemCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với id = {request.Id}");
        }

        return new ItemCategoryDto
        {
            Id = category.Id,
            Code = category.Code.ToString(),
            Name = category.Name,
            Quantity = category.Quantity,
            Description = category.Description
        };
    }
}
