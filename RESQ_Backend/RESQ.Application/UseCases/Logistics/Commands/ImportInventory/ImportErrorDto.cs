namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportErrorDto
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}