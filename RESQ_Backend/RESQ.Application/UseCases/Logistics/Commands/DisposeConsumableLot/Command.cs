using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.DisposeConsumableLot;

public record DisposeConsumableLotCommand(
    Guid UserId,
    int LotId,
    int Quantity,
    string Reason,
    string? Note,
    int? DepotId = null) : IRequest<DisposeConsumableLotResponse>;
