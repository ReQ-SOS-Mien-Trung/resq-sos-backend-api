using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

public class UpdateServiceZoneCommandHandler(
    IServiceZoneRepository serviceZoneRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateServiceZoneCommand, UpdateServiceZoneResponse>
{
    private readonly IServiceZoneRepository _serviceZoneRepository = serviceZoneRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UpdateServiceZoneResponse> Handle(UpdateServiceZoneCommand request, CancellationToken cancellationToken)
    {
        var existing = await _serviceZoneRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vùng phục vụ với Id={request.Id} không tồn tại.");

        existing.Name = request.Name;
        existing.Coordinates = request.Coordinates
            .Select(c => new CoordinatePoint { Latitude = c.Latitude, Longitude = c.Longitude })
            .ToList();
        existing.IsActive = request.IsActive;
        existing.UpdatedBy = request.UpdatedBy;
        existing.UpdatedAt = DateTime.UtcNow;

        await _serviceZoneRepository.UpdateAsync(existing, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateServiceZoneResponse
        {
            Id = existing.Id,
            Name = existing.Name,
            Coordinates = existing.Coordinates
                .Select(c => new CoordinatePointDto { Latitude = c.Latitude, Longitude = c.Longitude })
                .ToList(),
            IsActive = existing.IsActive,
            UpdatedAt = existing.UpdatedAt
        };
    }
}
