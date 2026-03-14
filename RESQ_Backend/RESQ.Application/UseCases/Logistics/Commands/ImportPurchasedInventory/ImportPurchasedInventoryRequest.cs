namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryRequest
{
    public VatInvoiceDto VatInvoice { get; set; } = new();
    public List<ImportPurchasedItemDto> Items { get; set; } = new();
}
