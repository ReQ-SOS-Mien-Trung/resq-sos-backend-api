using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots
{
    public class GetAllDepotsQueryHandler(IDepotRepository depotRepository, ILogger<GetAllDepotsQueryHandler> logger) : IRequestHandler<GetAllDepotsQuery, GetAllDepotsResponse>
    {
        private readonly IDepotRepository _depotRepository = depotRepository;
        private readonly ILogger<GetAllDepotsQueryHandler> _logger = logger;

        public async Task<GetAllDepotsResponse> Handle(GetAllDepotsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling {handler} - retrieving all depots page {page}", nameof(GetAllDepotsQueryHandler), request.PageNumber);

            // Use the Paged repository method
            var pagedResult = await _depotRepository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
            
            var dtos = pagedResult.Items.Select(depot => new DepotDto
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
            }).ToList();

            var response = new GetAllDepotsResponse
            {
                Items = dtos,
                PageNumber = pagedResult.PageNumber,
                PageSize = pagedResult.PageSize,
                TotalCount = pagedResult.TotalCount,
                TotalPages = pagedResult.TotalPages,
                HasNextPage = pagedResult.HasNextPage,
                HasPreviousPage = pagedResult.HasPreviousPage
            };

            _logger.LogInformation("{handler} - retrieved {count} depots on page {page}", nameof(GetAllDepotsQueryHandler), dtos.Count, request.PageNumber);
            return response;
        }
    }
}
