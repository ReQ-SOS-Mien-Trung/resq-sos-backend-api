using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

public class GetServiceZoneQueryHandler(
    IServiceZoneRepository serviceZoneRepository,
    IServiceZoneSummaryRepository serviceZoneSummaryRepository)
    : IRequestHandler<GetServiceZoneQuery, List<GetServiceZoneResponse>>,
      IRequestHandler<GetServiceZoneByIdQuery, GetServiceZoneResponse>,
      IRequestHandler<GetAllServiceZoneQuery, List<GetServiceZoneResponse>>
{
    private readonly IServiceZoneRepository _serviceZoneRepository = serviceZoneRepository;
    private readonly IServiceZoneSummaryRepository _serviceZoneSummaryRepository = serviceZoneSummaryRepository;

    public async Task<List<GetServiceZoneResponse>> Handle(GetServiceZoneQuery request, CancellationToken cancellationToken)
    {
        var zones = await _serviceZoneRepository.GetAllActiveAsync(cancellationToken);
        if (zones.Count == 0)
            throw new NotFoundException("Chưa có vùng phục vụ nào đang active.");

        return await ToResponsesAsync(zones, cancellationToken);
    }

    public async Task<GetServiceZoneResponse> Handle(GetServiceZoneByIdQuery request, CancellationToken cancellationToken)
    {
        var zone = await _serviceZoneRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vùng phục vụ với Id={request.Id} không tồn tại.");

        var counts = await _serviceZoneSummaryRepository.GetResourceCountsAsync([zone], cancellationToken);
        counts.TryGetValue(zone.Id, out var zoneCounts);

        return ToResponse(zone, zoneCounts);
    }

    public async Task<List<GetServiceZoneResponse>> Handle(GetAllServiceZoneQuery request, CancellationToken cancellationToken)
    {
        var zones = await _serviceZoneRepository.GetAllAsync(cancellationToken);
        return await ToResponsesAsync(zones, cancellationToken);
    }

    private async Task<List<GetServiceZoneResponse>> ToResponsesAsync(
        List<ServiceZoneModel> zones,
        CancellationToken cancellationToken)
    {
        var counts = await _serviceZoneSummaryRepository.GetResourceCountsAsync(zones, cancellationToken);

        return zones
            .Select(zone =>
            {
                counts.TryGetValue(zone.Id, out var zoneCounts);
                return ToResponse(zone, zoneCounts);
            })
            .ToList();
    }

    private static GetServiceZoneResponse ToResponse(ServiceZoneModel zone, ServiceZoneResourceCounts? counts) =>
        new()
        {
            Id = zone.Id,
            Name = zone.Name,
            Coordinates = zone.Coordinates
                .Select(c => new CoordinatePointDto { Latitude = c.Latitude, Longitude = c.Longitude })
                .ToList(),
            IsActive = zone.IsActive,
            Counts = ToCountsDto(counts),
            UpdatedBy = zone.UpdatedBy,
            CreatedAt = zone.CreatedAt,
            UpdatedAt = zone.UpdatedAt
        };

    private static ServiceZoneCountsDto ToCountsDto(ServiceZoneResourceCounts? counts) =>
        counts is null
            ? new ServiceZoneCountsDto()
            : new ServiceZoneCountsDto
            {
                PendingSosRequestCount = counts.PendingSosRequestCount,
                IncidentSosRequestCount = counts.IncidentSosRequestCount,
                TeamIncidentCount = counts.TeamIncidentCount,
                AssemblyPointCount = counts.AssemblyPointCount,
                DepotCount = counts.DepotCount
            };
}
