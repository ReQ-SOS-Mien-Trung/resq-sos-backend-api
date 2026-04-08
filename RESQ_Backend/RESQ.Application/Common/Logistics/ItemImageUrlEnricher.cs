using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.Common.Logistics;

internal static class ItemImageUrlEnricher
{
    public static async Task EnrichAsync<T>(
        IEnumerable<T> items,
        Func<T, int?> itemIdSelector,
        Action<T, string?> imageUrlAssigner,
        IItemModelMetadataRepository itemModelMetadataRepository,
        CancellationToken cancellationToken)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return;

        var itemIds = itemList
            .Select(itemIdSelector)
            .Where(itemId => itemId.HasValue)
            .Select(itemId => itemId!.Value)
            .Distinct()
            .ToArray();

        if (itemIds.Length == 0)
            return;

        var itemModels = await itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);

        foreach (var item in itemList)
        {
            var itemId = itemIdSelector(item);
            if (!itemId.HasValue)
                continue;

            if (itemModels.TryGetValue(itemId.Value, out var itemModel))
            {
                imageUrlAssigner(item, itemModel.ImageUrl);
            }
        }
    }
}