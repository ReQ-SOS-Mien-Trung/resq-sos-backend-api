namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public record ExportInventoryRequest(
    int ItemModelId,
    int Quantity,
    string? Note);
