using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileType;

public class UpdateDocumentFileTypeCommandHandler(
    IDocumentFileTypeRepository documentFileTypeRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateDocumentFileTypeCommandHandler> logger
) : IRequestHandler<UpdateDocumentFileTypeCommand, UpdateDocumentFileTypeResponse>
{
    private readonly IDocumentFileTypeRepository _documentFileTypeRepository = documentFileTypeRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateDocumentFileTypeCommandHandler> _logger = logger;

    public async Task<UpdateDocumentFileTypeResponse> Handle(UpdateDocumentFileTypeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating DocumentFileType Id={Id}", request.Id);

        var existing = await _documentFileTypeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            throw new NotFoundException("Loại tài liệu", request.Id);
        }

        // Check if code already taken by another record
        var byCode = await _documentFileTypeRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (byCode is not null && byCode.Id != request.Id)
        {
            throw new ConflictException($"Mã loại tài liệu '{request.Code}' đã tồn tại");
        }

        var model = new DocumentFileTypeModel
        {
            Id = request.Id,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        };

        await _documentFileTypeRepository.UpdateAsync(model, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("DocumentFileType updated: Id={Id}, Code={Code}", request.Id, request.Code);

        return new UpdateDocumentFileTypeResponse
        {
            Id = request.Id,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            Message = "Cập nhật loại tài liệu thành công."
        };
    }
}
