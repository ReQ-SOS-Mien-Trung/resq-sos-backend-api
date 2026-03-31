namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryRequest
{
    public string? AdvancedByName { get; set; }
    public List<ImportPurchaseGroupDto> Invoices { get; set; } = new();
}
