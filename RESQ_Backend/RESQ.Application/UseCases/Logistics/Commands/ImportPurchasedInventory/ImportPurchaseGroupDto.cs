namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchaseGroupDto
{
    public VatInvoiceDto VatInvoice { get; set; } = new();
    public List<ImportPurchasedItemDto> Items { get; set; } = new();
}
