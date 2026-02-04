using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;

public record UpdateAssemblyPointCommand(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    int CapacityTeams
) : IRequest;
