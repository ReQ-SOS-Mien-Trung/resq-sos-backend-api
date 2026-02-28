using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypes;

public record GetAllDocumentFileTypesQuery(bool? ActiveOnly = true) : IRequest<GetAllDocumentFileTypesResponse>;
