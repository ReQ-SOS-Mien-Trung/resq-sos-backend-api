using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypeCategories;

public class GetAllDocumentFileTypeCategoriesQueryHandler(
    IDocumentFileTypeCategoryRepository repository,
    ILogger<GetAllDocumentFileTypeCategoriesQueryHandler> logger)
    : IRequestHandler<GetAllDocumentFileTypeCategoriesQuery, GetAllDocumentFileTypeCategoriesResponse>
{
    private readonly IDocumentFileTypeCategoryRepository _repository = repository;
    private readonly ILogger<GetAllDocumentFileTypeCategoriesQueryHandler> _logger = logger;

    public async Task<GetAllDocumentFileTypeCategoriesResponse> Handle(GetAllDocumentFileTypeCategoriesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving all document file type categories", nameof(GetAllDocumentFileTypeCategoriesQueryHandler));

        var categories = await _repository.GetAllAsync(cancellationToken);

        var dtos = categories.Select(c => new DocumentFileTypeCategoryItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Description = c.Description
        }).ToList();

        _logger.LogInformation("{handler} - retrieved {count} document file type categories", nameof(GetAllDocumentFileTypeCategoriesQueryHandler), dtos.Count);

        return new GetAllDocumentFileTypeCategoriesResponse { Items = dtos };
    }
}
