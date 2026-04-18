using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypes;

public class GetAllDocumentFileTypesQueryHandler(
    IDocumentFileTypeRepository documentFileTypeRepository
) : IRequestHandler<GetAllDocumentFileTypesQuery, GetAllDocumentFileTypesResponse>
{
    private readonly IDocumentFileTypeRepository _documentFileTypeRepository = documentFileTypeRepository;

    public async Task<GetAllDocumentFileTypesResponse> Handle(GetAllDocumentFileTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _documentFileTypeRepository.GetAllAsync(request.ActiveOnly, cancellationToken);

        return new GetAllDocumentFileTypesResponse
        {
            Items = items.Select(x => new DocumentFileTypeDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                DocumentFileTypeCategoryId = x.DocumentFileTypeCategoryId,
                DocumentFileTypeCategory = x.DocumentFileTypeCategory is not null
                    ? new DocumentFileTypeCategoryDto
                    {
                        Id = x.DocumentFileTypeCategory.Id,
                        Code = x.DocumentFileTypeCategory.Code,
                        Description = x.DocumentFileTypeCategory.Description
                    }
                    : null
            }).ToList()
        };
    }
}
