using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IItemModelMetadataRepository
{
    /// <summary>
    /// Returns all item models as lightweight key/value pairs for dropdown metadata.
    /// Key = item model ID, Value = item model name.
    /// </summary>
    Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns item models filtered by category code as lightweight key/value pairs.
    /// Key = item model ID, Value = item model name.
    /// </summary>
    Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all item models with target groups, unit, item type, and category code
    /// for the donation import Excel template.
    /// </summary>
    Task<List<DonationImportItemInfo>> GetAllForDonationTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-fetches item models by their IDs with TargetGroups eagerly loaded.
    /// Chunks at 500 IDs to avoid SQL parameter limits. Returns a dictionary keyed by ID.
    /// </summary>
    Task<Dictionary<int, ItemModelRecord>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a category exists.
    /// </summary>
    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the item model has generated any inventory transactions.
    /// </summary>
    Task<bool> HasInventoryTransactionsAsync(int itemModelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing item model and its target groups.
    /// Returns false when item model does not exist.
    /// </summary>
    Task<bool> UpdateItemModelAsync(ItemModelRecord model, CancellationToken cancellationToken = default);
}
