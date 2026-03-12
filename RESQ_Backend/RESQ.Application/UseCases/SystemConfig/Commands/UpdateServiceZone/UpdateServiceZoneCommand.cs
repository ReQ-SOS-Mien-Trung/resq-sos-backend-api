using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

public record UpdateServiceZoneCommand(
    int Id,
    string Name,
    List<CoordinatePointDto> Coordinates,
    bool IsActive,
    Guid UpdatedBy
) : IRequest<UpdateServiceZoneResponse>;

public class CoordinatePointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
