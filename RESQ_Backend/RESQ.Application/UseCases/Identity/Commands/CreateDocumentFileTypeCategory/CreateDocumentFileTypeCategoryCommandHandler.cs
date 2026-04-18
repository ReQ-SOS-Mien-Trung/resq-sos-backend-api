using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileTypeCategory;

public class CreateDocumentFileTypeCategoryCommandHandler(
    IDocumentFileTypeCategoryRepository repository)
    : IRequestHandler<CreateDocumentFileTypeCategoryCommand, CreateDocumentFileTypeCategoryResponse>
{
    private readonly IDocumentFileTypeCategoryRepository _repository = repository;

    public async Task<CreateDocumentFileTypeCategoryResponse> Handle(CreateDocumentFileTypeCategoryCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Danh mục loại tài liệu với code '{request.Code}' đã tồn tại.");

        var model = DocumentFileTypeCategoryModel.Create(request.Code, request.Description);
        var id = await _repository.CreateAsync(model, cancellationToken);

        if (id == 0)
            throw new CreateFailedException("danh mục loại tài liệu");

        var created = await _repository.GetByIdAsync(id, cancellationToken);

        return new CreateDocumentFileTypeCategoryResponse
        {
            Id = created!.Id,
            Code = created.Code,
            Description = created.Description
        };
    }
}
