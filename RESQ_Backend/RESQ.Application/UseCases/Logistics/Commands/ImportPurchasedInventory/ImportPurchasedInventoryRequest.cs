namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryRequest
{
    public List<ImportPurchaseGroupDto> Invoices { get; set; } = new();
}
