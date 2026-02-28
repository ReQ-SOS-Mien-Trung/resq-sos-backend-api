using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileType;

public class CreateDocumentFileTypeCommandHandler(
    IDocumentFileTypeRepository documentFileTypeRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateDocumentFileTypeCommandHandler> logger
) : IRequestHandler<CreateDocumentFileTypeCommand, CreateDocumentFileTypeResponse>
{
    private readonly IDocumentFileTypeRepository _documentFileTypeRepository = documentFileTypeRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateDocumentFileTypeCommandHandler> _logger = logger;

    public async Task<CreateDocumentFileTypeResponse> Handle(CreateDocumentFileTypeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating DocumentFileType Code={Code}", request.Code);

        // Check if code already exists
        var existing = await _documentFileTypeRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"Mã loại tài liệu '{request.Code}' đã tồn tại");
        }

        var model = new DocumentFileTypeModel
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        };

        var id = await _documentFileTypeRepository.CreateAsync(model, cancellationToken);

        _logger.LogInformation("DocumentFileType created: Id={Id}, Code={Code}", id, request.Code);

        return new CreateDocumentFileTypeResponse
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            Message = "Tạo loại tài liệu thành công."
        };
    }
}
