using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;

public class CreateDepotCommandHandler(IDepotRepository depotRepository, IUnitOfWork unitOfWork, ILogger<CreateDepotCommandHandler> logger) : IRequestHandler<CreateDepotCommand, CreateDepotResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateDepotCommandHandler> _logger = logger;

    public async Task<CreateDepotResponse> Handle(CreateDepotCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateDepotCommand for Name={name}", request.Name);
        
        // 1. Validate Duplicate Name (Application Layer Validation)
        var existing = await _depotRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new DepotNameDuplicatedException(request.Name);
        }

        // 2. Create Domain Model
        // Note: GeoLocation validation remains in Domain, handled by DomainExceptionBehaviour if it fails
        var location = new GeoLocation(request.Latitude, request.Longitude);

        var depot = DepotModel.Create(
            request.Name,
            request.Address,
            location,
            request.Capacity
        );

        await _depotRepository.CreateAsync(depot, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();
        
        if (succeedCount < 1) 
            throw new CreateFailedException("Kho");

        var addedDepot = await _depotRepository.GetByNameAsync(request.Name, cancellationToken); 
        if (addedDepot is null) throw new CreateFailedException("Kho");

        return new CreateDepotResponse
        {
            Id = addedDepot.Id,
            Name = addedDepot.Name,
            Address = addedDepot.Address,
            Latitude = addedDepot.Location?.Latitude,
            Longitude = addedDepot.Location?.Longitude,
            Capacity = addedDepot.Capacity,
            CurrentUtilization = addedDepot.CurrentUtilization,
            Status = addedDepot.Status.ToString(),
            DepotManagerId = addedDepot.CurrentManagerId,
            LastUpdatedAt = addedDepot.LastUpdatedAt
        };
    }
}
