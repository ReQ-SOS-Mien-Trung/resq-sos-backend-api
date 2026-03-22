using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;

/// <summary>
/// Check-in tại sự kiện triệu tập. Yêu cầu GPS (latitude, longitude) để validate vị trí.
/// </summary>
public record CheckInAtAssemblyPointCommand(
    int AssemblyEventId,
    Guid UserId,
    double Latitude,
    double Longitude) : IRequest;
