namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsRequest
{
    public int DepotId { get; set; }
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string? BatchNote { get; set; }
    public List<ImportReliefItemDto> Items { get; set; } = new();
}
