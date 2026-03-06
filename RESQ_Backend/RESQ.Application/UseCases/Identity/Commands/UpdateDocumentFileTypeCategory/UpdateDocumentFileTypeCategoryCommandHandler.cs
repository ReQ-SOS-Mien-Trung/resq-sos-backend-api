using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileTypeCategory;

public class UpdateDocumentFileTypeCategoryCommandHandler(
    IDocumentFileTypeCategoryRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDocumentFileTypeCategoryCommand>
{
    private readonly IDocumentFileTypeCategoryRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(UpdateDocumentFileTypeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy danh mục loại tài liệu với id = {request.Id}");

        var duplicate = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (duplicate is not null && duplicate.Id != request.Id)
            throw new ConflictException($"Danh mục loại tài liệu với code '{request.Code}' đã tồn tại.");

        category.Update(request.Code, request.Description);

        await _repository.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveAsync();
    }
}
