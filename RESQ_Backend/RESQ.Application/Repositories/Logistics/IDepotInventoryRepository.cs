using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IDepotInventoryRepository
{
    Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId,
        List<int>? categoryIds,
        List<ItemType>? itemTypes,
        List<TargetGroup>? targetGroups,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng số lượng tồn kho theo danh mục của một kho cụ thể.
    /// </summary>
    Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm vật tư theo từ khoá danh mục/loại để agent AI dùng trong quá trình lập kế hoạch.
    /// </summary>
    Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
