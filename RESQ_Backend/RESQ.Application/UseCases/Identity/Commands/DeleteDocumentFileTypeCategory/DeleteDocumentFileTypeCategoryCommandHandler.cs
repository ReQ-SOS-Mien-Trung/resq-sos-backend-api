using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteDocumentFileTypeCategory;

public class DeleteDocumentFileTypeCategoryCommandHandler(
    IDocumentFileTypeCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteDocumentFileTypeCategoryCommand>
{
    private readonly IDocumentFileTypeCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteDocumentFileTypeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy danh mục loại tài liệu với id = {request.Id}");

        await _repository.DeleteAsync(category.Id, cancellationToken);
        await _unitOfWork.SaveAsync();
    }
}
