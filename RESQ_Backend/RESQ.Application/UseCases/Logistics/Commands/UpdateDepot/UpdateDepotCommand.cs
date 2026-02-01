using MediatR;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public record UpdateDepotCommand(
    int Id,
    string Name,
    string Address,
    GeoLocation Location,
    int Capacity
) : IRequest;
