using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.DeleteItemCategory;

public class DeleteItemCategoryCommandHandler(
    IItemCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteItemCategoryCommand>
{
    private readonly IItemCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteItemCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với id = {request.Id}");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();
    }
}