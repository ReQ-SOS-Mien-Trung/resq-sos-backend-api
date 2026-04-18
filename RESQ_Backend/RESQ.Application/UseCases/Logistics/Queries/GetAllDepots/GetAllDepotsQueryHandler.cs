using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots
{
    public class GetAllDepotsQueryHandler(
        IDepotRepository depotRepository,
        ISupplyRequestRepository supplyRequestRepository,
        ILogger<GetAllDepotsQueryHandler> logger) 
        : IRequestHandler<GetAllDepotsQuery, GetAllDepotsResponse>
    {
        private readonly IDepotRepository _depotRepository = depotRepository;
        private readonly ISupplyRequestRepository _supplyRequestRepository = supplyRequestRepository;
        private readonly ILogger<GetAllDepotsQueryHandler> _logger = logger;

        public async Task<GetAllDepotsResponse> Handle(GetAllDepotsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling {handler} - retrieving all depots page {page}", nameof(GetAllDepotsQueryHandler), request.PageNumber);

            var pagedResult = await _depotRepository.GetAllPagedAsync(request.PageNumber, request.PageSize, request.Statuses, request.Search, cancellationToken);

            var depotIds = pagedResult.Items.Select(d => d.Id).ToList();

            // Fetch tất cả supply requests (2 chiều) cho tất cả depot trong trang hiện tại
            var allRequests = depotIds.Count > 0
                ? await _supplyRequestRepository.GetRequestsByDepotIdsAsync(depotIds, cancellationToken)
                : [];

            // Group theo từng depotId để lookup nhanh
            var requestsByDepot = allRequests
                .SelectMany(r => new[]
                {
                    (DepotId: r.RequestingDepotId, Request: r, Role: "Requester"),
                    (DepotId: r.SourceDepotId,     Request: r, Role: "Source")
                })
                .Where(x => depotIds.Contains(x.DepotId))
                .GroupBy(x => x.DepotId)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            var dtos = pagedResult.Items.Select(depot => 
            {
                var manager = depot.CurrentManager;

                requestsByDepot.TryGetValue(depot.Id, out var depotRequests);

                return new DepotDto
                {
                    Id = depot.Id,
                    Name = depot.Name,
                    Address = depot.Address,
                    Latitude = depot.Location?.Latitude,
                    Longitude = depot.Location?.Longitude,
                    Capacity = depot.Capacity,
                    CurrentUtilization = depot.CurrentUtilization,
                    WeightCapacity = depot.WeightCapacity,
                    CurrentWeightUtilization = depot.CurrentWeightUtilization,
                    Status = depot.Status.ToString(),
                    
                    // Map Manager details
                    Manager = manager != null ? new ManagerDto
                    {
                        Id = manager.UserId,
                        FirstName = manager.FirstName,
                        LastName = manager.LastName,
                        Email = manager.Email,
                        Phone = manager.Phone
                    } : null,
                    
                    ImageUrl = depot.ImageUrl,
                    LastUpdatedAt = depot.LastUpdatedAt,

                    Requests = depotRequests?
                        .Select(x => new DepotRequestDto
                        {
                            Id                  = x.Request.Id,
                            RequestingDepotId   = x.Request.RequestingDepotId,
                            RequestingDepotName = x.Request.RequestingDepotName,
                            SourceDepotId       = x.Request.SourceDepotId,
                            SourceDepotName     = x.Request.SourceDepotName,
                            Role                = x.Role,
                            PriorityLevel       = x.Request.PriorityLevel,
                            SourceStatus        = x.Request.SourceStatus,
                            RequestingStatus    = x.Request.RequestingStatus,
                            CreatedAt           = x.Request.CreatedAt,
                            AutoRejectAt        = x.Request.AutoRejectAt,
                            ShippedAt           = x.Request.ShippedAt,
                            CompletedAt         = x.Request.CompletedAt
                        })
                        .ToList() ?? []
                };
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
