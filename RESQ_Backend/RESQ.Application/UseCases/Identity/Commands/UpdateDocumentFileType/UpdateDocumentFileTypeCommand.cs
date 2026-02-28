using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileType;

public record UpdateDocumentFileTypeCommand(
    int Id,
    string Code,
    string? Name,
    string? Description,
    bool IsActive
) : IRequest<UpdateDocumentFileTypeResponse>;
