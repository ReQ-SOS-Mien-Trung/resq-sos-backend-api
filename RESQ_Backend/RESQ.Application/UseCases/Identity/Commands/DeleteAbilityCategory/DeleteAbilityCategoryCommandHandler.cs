using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteAbilityCategory;

public class DeleteAbilityCategoryCommandHandler(
    IAbilityCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAbilityCategoryCommand>
{
    private readonly IAbilityCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteAbilityCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy danh mục ability với id = {request.Id}");

        await _repository.DeleteAsync(category.Id, cancellationToken);
        await _unitOfWork.SaveAsync();
    }
}
