namespace RESQ.Application.Repositories.Logistics;

/// <summary>DTO dùng để tạo bản ghi xử lý tồn kho bên ngoài.</summary>
public record CreateClosureExternalItemDto(
    int DepotId,
    int? ClosureId,
    int? ItemModelId,
    int? LotId,
    int? ReusableItemId,
    string ItemName,
    string? CategoryName,
    string ItemType,
    string? Unit,
    string? SerialNumber,
    int Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    string HandlingMethod,
    string? Recipient,
    string? Note,
    string? ImageUrl,
    Guid ProcessedBy,
    DateTime ProcessedAt);

public class DepotClosureExternalItemDetailDto
{
    public int Id { get; set; }
    public int? ItemModelId { get; set; }
    public int? LotId { get; set; }
    public int? ReusableItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? SerialNumber { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string HandlingMethod { get; set; } = string.Empty;
    public string HandlingMethodDisplay { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Note { get; set; }
    public string? ImageUrl { get; set; }
    public Guid ProcessedBy { get; set; }
    public DateTime ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IDepotClosureExternalItemRepository
{
    Task CreateBulkAsync(IEnumerable<CreateClosureExternalItemDto> items, CancellationToken cancellationToken = default);
    Task<List<DepotClosureExternalItemDetailDto>> GetByClosureIdAsync(int closureId, CancellationToken cancellationToken = default);
}
