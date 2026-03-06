using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypeCategories;

public record GetAllDocumentFileTypeCategoriesQuery : IRequest<GetAllDocumentFileTypeCategoriesResponse>;
