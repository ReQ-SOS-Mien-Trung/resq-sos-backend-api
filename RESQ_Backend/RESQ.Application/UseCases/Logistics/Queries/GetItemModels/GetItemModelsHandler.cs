using MediatR;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemModels;

public class GetItemModelsHandler(IItemModelMetadataRepository repository)
    : IRequestHandler<GetItemModelsQuery, List<ItemModelDetailDto>>
{
    private readonly IItemModelMetadataRepository _repository = repository;

    public async Task<List<ItemModelDetailDto>> Handle(GetItemModelsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllDetailAsync(cancellationToken);

        if (request.CategoryId.HasValue)
            items = items.Where(x => x.CategoryId == request.CategoryId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(request.ItemType))
            items = items.Where(x => x.ItemType.Equals(request.ItemType.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        return items;
    }
}
