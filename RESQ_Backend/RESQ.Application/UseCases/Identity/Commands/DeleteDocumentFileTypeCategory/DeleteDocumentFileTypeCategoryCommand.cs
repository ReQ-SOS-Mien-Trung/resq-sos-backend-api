using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteDocumentFileTypeCategory;

public record DeleteDocumentFileTypeCategoryCommand(int Id) : IRequest;
