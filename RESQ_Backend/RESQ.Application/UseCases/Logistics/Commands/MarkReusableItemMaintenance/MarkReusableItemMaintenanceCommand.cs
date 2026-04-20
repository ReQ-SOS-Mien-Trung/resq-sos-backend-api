using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemMaintenance;

public record MarkReusableItemMaintenanceCommand(
    Guid UserId,
    int DepotId,
    int ReusableItemId,
    string? Note) : IRequest<MarkReusableItemMaintenanceResponse>;
