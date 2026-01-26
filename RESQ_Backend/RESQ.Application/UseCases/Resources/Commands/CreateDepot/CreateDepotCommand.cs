using MediatR;
using RESQ.Domain.Entities.Resources.ValueObjects;

namespace RESQ.Application.UseCases.Resources.Commands.CreateDepot;
public record CreateDepotCommand (
    string Name,
    string Address,
    GeoLocation Location,
    int Capacity,
    Guid? DepotManagerId
    ) : IRequest<CreateDepotResponse>;
