using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileTypeCategory;

public record UpdateDocumentFileTypeCategoryCommand(int Id, string Code, string? Description) : IRequest;
