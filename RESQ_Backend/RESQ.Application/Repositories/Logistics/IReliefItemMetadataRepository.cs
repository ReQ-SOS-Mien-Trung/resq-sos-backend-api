using RESQ.Application.Common.Models;
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
}
