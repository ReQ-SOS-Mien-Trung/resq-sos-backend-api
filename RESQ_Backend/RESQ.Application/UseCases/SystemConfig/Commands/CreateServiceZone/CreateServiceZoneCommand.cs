using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateServiceZone;

public record CreateServiceZoneCommand(
    string Name,
    List<CoordinatePointDto> Coordinates,
    bool IsActive,
    Guid CreatedBy
) : IRequest<CreateServiceZoneResponse>;
