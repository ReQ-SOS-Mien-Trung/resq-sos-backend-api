using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Resources;
using RESQ.Application.UseCases.Resources.Queries.Depot;

namespace RESQ.Application.UseCases.Resources.Queries.GetAllDepots
{
    public class GetAllDepotsQueryHandler(IDepotRepository depotRepository, ILogger<GetAllDepotsQueryHandler> logger) : IRequestHandler<GetAllDepotsQuery, GetAllDepotsResponse>
    {
        private readonly IDepotRepository _depotRepository = depotRepository;
        private readonly ILogger<GetAllDepotsQueryHandler> _logger = logger;

        public async Task<GetAllDepotsResponse> Handle(GetAllDepotsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling {handler} - retrieving all depots", nameof(GetAllDepotsQueryHandler));

            var depots = await _depotRepository.GetAllAsync(cancellationToken);
            var response = new GetAllDepotsResponse
            {
                Depots = depots.Select(depot => new DepotDto
                {
                    Id = depot.Id,
                    Name = depot.Name,
                    Address = depot.Address,
                    Latitude = depot.Latitude,
                    Longitude = depot.Longitude,
                    Capacity = depot.Capacity,
                    CurrentUtilization = depot.CurrentUtilization,
                    Status = depot.Status.ToString(),
                    DepotManagerId = depot.DepotManagerId,
                    LastUpdatedAt = depot.LastUpdatedAt
                }).ToList()
            };

            _logger.LogInformation("{handler} - retrieved {count} depots", nameof(GetAllDepotsQueryHandler), depots.Count());
            return response;
        }
    }
}
