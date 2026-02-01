using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Resources;
using RESQ.Domain.Entities.Resources;

namespace RESQ.Application.UseCases.Resources.Commands.CreateDepot;

public class CreateDepotCommandHandler(IDepotRepository depotRepository, IUnitOfWork unitOfWork, ILogger<CreateDepotCommandHandler> logger) : IRequestHandler<CreateDepotCommand, CreateDepotResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateDepotCommandHandler> _logger = logger;

    public async Task<CreateDepotResponse> Handle(CreateDepotCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateDepotCommand for Name={name}", request.Name);
        
        var depot = DepotModel.Create(
            request.Name,
            request.Address,
            request.Location,
            request.Capacity
        );

        await _depotRepository.CreateAsync(depot, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();
        
        if (succeedCount < 1) 
            throw new CreateFailedException("Depot");

        // Fetching back to get ID and confirm persistence
        var createdDepots = await _depotRepository.GetAllAsync(cancellationToken); 
        var addedDepot = createdDepots
            .Where(d => d.Name == request.Name && d.Address == request.Address)
            .OrderByDescending(d => d.LastUpdatedAt)
            .FirstOrDefault();

        if (addedDepot is null) throw new CreateFailedException("Depot");

        _logger.LogInformation("Created depot: Id={id} Name={name}", addedDepot.Id, request.Name);

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
            DepotManagerId = addedDepot.CurrentManagerId, // Use computed property
            LastUpdatedAt = addedDepot.LastUpdatedAt
        };
    }
}
