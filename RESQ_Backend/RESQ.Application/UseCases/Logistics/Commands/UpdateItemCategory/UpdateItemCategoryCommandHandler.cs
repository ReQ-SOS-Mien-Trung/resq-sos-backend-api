using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemCategory;

public class UpdateItemCategoryCommandHandler(
    IItemCategoryRepository itemCategoryRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateItemCategoryCommand, Unit>
{
    private readonly IItemCategoryRepository _itemCategoryRepository = itemCategoryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Unit> Handle(UpdateItemCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _itemCategoryRepository.GetByIdAsync(request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException("ItemCategory", request.Id);
        }

        // Preserve existing Code and update Name/Description
        category.Update(category.Code, request.Name, request.Description);

        await _itemCategoryRepository.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
