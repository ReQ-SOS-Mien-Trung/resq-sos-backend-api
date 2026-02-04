using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotById;

public class GetDepotByIdQueryHandler(
    IDepotRepository depotRepository, 
    ILogger<GetDepotByIdQueryHandler> logger) 
    : IRequestHandler<GetDepotByIdQuery, DepotDto>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ILogger<GetDepotByIdQueryHandler> _logger = logger;

    public async Task<DepotDto> Handle(GetDepotByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetDepotByIdQuery for Id={Id}", request.Id);

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken);

        if (depot == null)
        {
            throw new NotFoundException($"Không tìm thấy kho cứu trợ với id = {request.Id}");
        }

        return new DepotDto
        {
            Id = depot.Id,
            Name = depot.Name,
            Address = depot.Address,
            Latitude = depot.Location?.Latitude,
            Longitude = depot.Location?.Longitude,
            Capacity = depot.Capacity,
            CurrentUtilization = depot.CurrentUtilization,
            Status = depot.Status.ToString(),
            DepotManagerId = depot.CurrentManagerId,
            LastUpdatedAt = depot.LastUpdatedAt
        };
    }
}
