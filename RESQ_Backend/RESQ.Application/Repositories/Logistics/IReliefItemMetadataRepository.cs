using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IReliefItemMetadataRepository
{
    /// <summary>
    /// Returns all relief items as lightweight key/value pairs for dropdown metadata.
    /// Key = relief item ID, Value = relief item name.
    /// </summary>
    Task<List<MetadataDto>> GetAllForMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns relief items filtered by category code as lightweight key/value pairs.
    /// Key = relief item ID, Value = relief item name.
    /// </summary>
    Task<List<MetadataDto>> GetByCategoryCodeAsync(ItemCategoryCode categoryCode, CancellationToken cancellationToken = default);
}
