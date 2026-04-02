using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public record ExportInventoryCommand(
    Guid UserId,
    int ItemModelId,
    int Quantity,
    string? Note) : IRequest<ExportInventoryResponse>;
