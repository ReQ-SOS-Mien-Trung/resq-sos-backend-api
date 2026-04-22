using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateServiceZone;

public class CreateServiceZoneCommandHandler(
    IServiceZoneRepository serviceZoneRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<CreateServiceZoneCommand, CreateServiceZoneResponse>
{
    private readonly IServiceZoneRepository _serviceZoneRepository = serviceZoneRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<CreateServiceZoneResponse> Handle(CreateServiceZoneCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var model = new ServiceZoneModel
        {
            Name = request.Name,
            Coordinates = request.Coordinates
                .Select(c => new CoordinatePoint { Latitude = c.Latitude, Longitude = c.Longitude })
                .ToList(),
            IsActive = request.IsActive,
            UpdatedBy = request.CreatedBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _serviceZoneRepository.CreateAsync(model, cancellationToken);

        var response = new CreateServiceZoneResponse
        {
            Id = model.Id,
            Name = model.Name,
            Coordinates = model.Coordinates
                .Select(c => new CoordinatePointDto { Latitude = c.Latitude, Longitude = c.Longitude })
                .ToList(),
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt
        };

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityId = response.Id,
            EntityType = "ServiceZone",
            ConfigKey = "service-zones",
            Action = "Created",
            Status = response.IsActive ? "Active" : "Inactive",
            ChangedAt = model.UpdatedAt
        }, cancellationToken);

        return response;
    }
}
