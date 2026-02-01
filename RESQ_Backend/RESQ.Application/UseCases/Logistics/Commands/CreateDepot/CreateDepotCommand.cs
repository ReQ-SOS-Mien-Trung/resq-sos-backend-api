using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
public record CreateDepotCommand (
    string Name,
    string Address,
    GeoLocation Location,
    int Capacity
    ) : IRequest<CreateDepotResponse>;
