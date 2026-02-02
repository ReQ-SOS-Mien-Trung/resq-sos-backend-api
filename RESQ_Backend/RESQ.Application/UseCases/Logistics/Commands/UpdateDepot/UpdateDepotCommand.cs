using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public record UpdateDepotCommand(
    int Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int Capacity
) : IRequest;
