using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

public class GetServiceZoneQueryHandler(IServiceZoneRepository serviceZoneRepository)
    : IRequestHandler<GetServiceZoneQuery, GetServiceZoneResponse>,
      IRequestHandler<GetServiceZoneByIdQuery, GetServiceZoneResponse>
{
    private readonly IServiceZoneRepository _serviceZoneRepository = serviceZoneRepository;

    public async Task<GetServiceZoneResponse> Handle(GetServiceZoneQuery request, CancellationToken cancellationToken)
    {
        var zone = await _serviceZoneRepository.GetActiveAsync(cancellationToken)
            ?? throw new NotFoundException("Chưa có vùng phục vụ nào được cấu hình.");

        return ToResponse(zone);
    }

    public async Task<GetServiceZoneResponse> Handle(GetServiceZoneByIdQuery request, CancellationToken cancellationToken)
    {
        var zone = await _serviceZoneRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vùng phục vụ với Id={request.Id} không tồn tại.");

        return ToResponse(zone);
    }

    private static GetServiceZoneResponse ToResponse(RESQ.Domain.Entities.System.ServiceZoneModel zone) =>
        new()
        {
            Id = zone.Id,
            Name = zone.Name,
            Coordinates = zone.Coordinates
                .Select(c => new CoordinatePointDto { Latitude = c.Latitude, Longitude = c.Longitude })
                .ToList(),
            IsActive = zone.IsActive,
            UpdatedBy = zone.UpdatedBy,
            CreatedAt = zone.CreatedAt,
            UpdatedAt = zone.UpdatedAt
        };
}
