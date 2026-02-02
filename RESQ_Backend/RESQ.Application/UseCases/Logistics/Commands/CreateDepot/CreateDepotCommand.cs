using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
public record CreateDepotCommand (
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int Capacity
    ) : IRequest<CreateDepotResponse>;
