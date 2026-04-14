namespace RESQ.Application.Repositories.Logistics;

/// <summary>DTO dùng d? t?o b?n ghi x? lý t?n kho bên ngoài.</summary>
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

public class DepotClosureExternalItemDetailDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? Unit { get; set; }
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
