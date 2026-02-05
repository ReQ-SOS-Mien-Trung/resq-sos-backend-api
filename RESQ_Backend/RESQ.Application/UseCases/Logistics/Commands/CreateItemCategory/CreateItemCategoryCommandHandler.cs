using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateItemCategory;

public class CreateItemCategoryCommandHandler(
    IItemCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateItemCategoryCommand, CreateItemCategoryResponse>
{
    private readonly IItemCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<CreateItemCategoryResponse> Handle(CreateItemCategoryCommand request, CancellationToken cancellationToken)
    {
        // 1. Check Uniqueness
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing != null)
        {
            throw new ItemCategoryCodeDuplicatedException(request.Code);
        }

        // 2. Create Domain Model
        var category = ItemCategoryModel.Create(request.Code, request.Name, request.Description);

        // 3. Persist
        await _repository.CreateAsync(category, cancellationToken);
        var success = await _unitOfWork.SaveAsync();

        if (success < 1)
        {
            throw new CreateFailedException("Danh mục vật phẩm");
        }

        // 4. Retrieve & Return
        var createdCategory = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        
        return new CreateItemCategoryResponse
        {
            Id = createdCategory!.Id,
            Code = createdCategory.Code.ToString(),
            Name = createdCategory.Name,
            Description = createdCategory.Description,
            Quantity = createdCategory.Quantity,
            CreatedAt = createdCategory.CreatedAt,
            UpdatedAt = createdCategory.UpdatedAt
        };
    }
}
