using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryByCode;

public class GetItemCategoryByCodeQueryHandler(IItemCategoryRepository repository)
    : IRequestHandler<GetItemCategoryByCodeQuery, ItemCategoryDto>
{
    private readonly IItemCategoryRepository _repository = repository;

    public async Task<ItemCategoryDto> Handle(GetItemCategoryByCodeQuery request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với mã = {request.Code}");
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