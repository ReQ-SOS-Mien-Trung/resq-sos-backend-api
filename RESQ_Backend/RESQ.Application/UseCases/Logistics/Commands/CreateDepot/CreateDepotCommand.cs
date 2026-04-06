using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
public record CreateDepotCommand (
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int Capacity,
    Guid? ManagerId = null,
    string? ImageUrl = null
    ) : IRequest<CreateDepotResponse>;
