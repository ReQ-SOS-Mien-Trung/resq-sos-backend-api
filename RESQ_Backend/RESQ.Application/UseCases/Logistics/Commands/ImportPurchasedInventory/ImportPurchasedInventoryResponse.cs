namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryResponse
{
    public int Imported { get; set; }
    public int Failed { get; set; }
    public int? VatInvoiceId { get; set; }
    public List<ImportPurchasedErrorDto> Errors { get; set; } = new();
}
