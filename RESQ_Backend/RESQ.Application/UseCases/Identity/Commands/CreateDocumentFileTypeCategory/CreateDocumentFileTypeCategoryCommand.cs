using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileTypeCategory;

public record CreateDocumentFileTypeCategoryCommand(string Code, string? Description) : IRequest<CreateDocumentFileTypeCategoryResponse>;
