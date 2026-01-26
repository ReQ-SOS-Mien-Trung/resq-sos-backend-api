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
        var depot = new DepotModel()
        {
            Name = request.Name,
            Address = request.Address,
            Location = request.Location,
            Capacity = request.Capacity,
            DepotManagerId = request.DepotManagerId
        };

        await _depotRepository.CreateAsync(depot);
        var succeedCount = await _unitOfWork.SaveAsync();
        if (succeedCount < 1) throw new CreateFailedException("Depot");

        var addedDepot = await _depotRepository.GetByIdAsync(depot.Id, cancellationToken);
        if (addedDepot is null) throw new CreateFailedException("Depot");

        _logger.LogInformation("Created depot: Id={id} Name={name}",depot.Id, request.Name);

        return new CreateDepotResponse
        {
            Id = addedDepot.Id,
            Name = addedDepot.Name,
            Address = addedDepot.Address,
            Latitude = addedDepot.Latitude,
            Longitude = addedDepot.Longitude,
            Capacity = addedDepot.Capacity,
            DepotManagerId = addedDepot.DepotManagerId,
            LastUpdatedAt = addedDepot.LastUpdatedAt
        };
    }
}
