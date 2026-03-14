namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryResponse
{
    public int TotalImported { get; set; }
    public int TotalFailed { get; set; }
    public List<ImportPurchaseGroupResultDto> Groups { get; set; } = new();
}
