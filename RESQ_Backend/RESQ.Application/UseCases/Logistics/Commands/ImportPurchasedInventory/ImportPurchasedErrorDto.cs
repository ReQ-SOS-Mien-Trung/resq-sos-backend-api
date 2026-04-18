namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedErrorDto
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}
