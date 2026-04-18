namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public record ExportInventoryRequest(
    int DepotId,
    int ItemModelId,
    int Quantity,
    string? Note);
