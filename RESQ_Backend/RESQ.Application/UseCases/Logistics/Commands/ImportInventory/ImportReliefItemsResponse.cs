namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsResponse
{
    public int Imported { get; set; }
    public int Failed { get; set; }
    public List<ImportErrorDto> Errors { get; set; } = new();
}