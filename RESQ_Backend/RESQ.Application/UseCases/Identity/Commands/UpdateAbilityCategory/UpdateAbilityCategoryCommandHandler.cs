using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateAbilityCategory;

public class UpdateAbilityCategoryCommandHandler(
    IAbilityCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAbilityCategoryCommand>
{
    private readonly IAbilityCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(UpdateAbilityCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy danh mục ability với id = {request.Id}");

        var duplicate = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (duplicate is not null && duplicate.Id != request.Id)
            throw new ConflictException($"Danh mục ability với code '{request.Code}' đã tồn tại.");

        category.Update(request.Code, request.Description);

        await _repository.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveAsync();
    }
}
