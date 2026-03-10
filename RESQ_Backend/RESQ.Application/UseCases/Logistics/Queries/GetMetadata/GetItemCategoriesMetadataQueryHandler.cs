using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetItemCategoriesMetadataQueryHandler(IItemCategoryRepository itemCategoryRepository) 
    : IRequestHandler<GetItemCategoriesMetadataQuery, List<MetadataDto>>
{
    private readonly IItemCategoryRepository _itemCategoryRepository = itemCategoryRepository;

    public async Task<List<MetadataDto>> Handle(GetItemCategoriesMetadataQuery request, CancellationToken cancellationToken)
    {
        var categories = await _itemCategoryRepository.GetAllAsync(cancellationToken);

        return categories.Select(c => new MetadataDto
        {
            Key = c.Id.ToString(),
            Value = c.Name
        }).ToList();
    }
}
