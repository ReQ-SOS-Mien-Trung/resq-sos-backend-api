using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public record CreateAssemblyPointCommand(
    string Name,
    double Latitude,
    double Longitude,
    int CapacityTeams
) : IRequest<CreateAssemblyPointResponse>;
