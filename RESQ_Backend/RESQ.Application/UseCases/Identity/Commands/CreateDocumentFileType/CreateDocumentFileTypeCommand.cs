using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileType;

public record CreateDocumentFileTypeCommand(
    string Code,
    string? Name,
    string? Description,
    bool IsActive = true
) : IRequest<CreateDocumentFileTypeResponse>;
