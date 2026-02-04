using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;

public class GetAllAssemblyPointsQueryHandler(
    IAssemblyPointRepository repository, 
    ILogger<GetAllAssemblyPointsQueryHandler> logger) 
    : IRequestHandler<GetAllAssemblyPointsQuery, GetAllAssemblyPointsResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly ILogger<GetAllAssemblyPointsQueryHandler> _logger = logger;

    public async Task<GetAllAssemblyPointsResponse> Handle(GetAllAssemblyPointsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving all assembly points page {page}", nameof(GetAllAssemblyPointsQueryHandler), request.PageNumber);

        var pagedResult = await _repository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedResult.Items.Select(ap => new AssemblyPointDto
        {
            Id = ap.Id,
            Code = ap.Code, // Added
            Name = ap.Name,
            Latitude = ap.Location?.Latitude,
            Longitude = ap.Location?.Longitude,
            CapacityTeams = ap.CapacityTeams,
            Status = ap.Status.ToString(),
            LastUpdatedAt = ap.UpdatedAt
        }).ToList();

        var response = new GetAllAssemblyPointsResponse
        {
            Items = dtos,
            PageNumber = pagedResult.PageNumber,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage
        };

        _logger.LogInformation("{handler} - retrieved {count} items on page {page}", nameof(GetAllAssemblyPointsQueryHandler), dtos.Count, request.PageNumber);
        return response;
    }
}
