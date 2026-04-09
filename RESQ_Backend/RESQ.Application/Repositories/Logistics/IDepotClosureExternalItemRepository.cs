namespace RESQ.Application.Repositories.Logistics;

/// <summary>DTO dùng để tạo bản ghi xử lý tồn kho bên ngoài.</summary>
public record CreateClosureExternalItemDto(
    int DepotId,
    int? ClosureId,
    string ItemName,
    string? CategoryName,
    string ItemType,
    string? Unit,
    int Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    string HandlingMethod,
    string? Recipient,
    string? Note,
    string? ImageUrl,
    Guid ProcessedBy,
    DateTime ProcessedAt);

public interface IDepotClosureExternalItemRepository
{
    /// <summary>Lưu danh sách bản ghi xử lý bên ngoài (bulk insert).</summary>
    Task CreateBulkAsync(IEnumerable<CreateClosureExternalItemDto> items, CancellationToken cancellationToken = default);
}
