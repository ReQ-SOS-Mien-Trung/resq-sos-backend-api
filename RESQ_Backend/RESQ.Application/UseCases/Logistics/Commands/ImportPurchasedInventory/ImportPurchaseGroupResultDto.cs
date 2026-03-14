namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchaseGroupResultDto
{
    public int GroupIndex { get; set; }
    public int? VatInvoiceId { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public List<ImportPurchasedErrorDto> Errors { get; set; } = new();
}
