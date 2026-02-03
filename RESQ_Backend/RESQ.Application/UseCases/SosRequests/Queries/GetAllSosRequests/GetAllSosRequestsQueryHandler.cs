using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.SosRequests;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetAllSosRequests;

public class GetAllSosRequestsQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ILogger<GetAllSosRequestsQueryHandler> logger
) : IRequestHandler<GetAllSosRequestsQuery, GetAllSosRequestsResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ILogger<GetAllSosRequestsQueryHandler> _logger = logger;

    public async Task<GetAllSosRequestsResponse> Handle(GetAllSosRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetAllSosRequestsQuery");

        var requests = await _sosRequestRepository.GetAllAsync(cancellationToken);

        return new GetAllSosRequestsResponse
        {
            SosRequests = requests.Select(x => new SosRequestDto
            {
                Id = x.Id,
                UserId = x.UserId,
                RawMessage = x.RawMessage,
                Status = x.Status,
                PriorityLevel = x.PriorityLevel,
                WaitTimeMinutes = x.WaitTimeMinutes,
                Latitude = x.Location?.Latitude,
                Longitude = x.Location?.Longitude,
                CreatedAt = x.CreatedAt,
                LastUpdatedAt = x.LastUpdatedAt
            }).ToList()
        };
    }
}