namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsRequest
{
    public int OrganizationId { get; set; }
    public List<ImportReliefItemDto> Items { get; set; } = new();
}