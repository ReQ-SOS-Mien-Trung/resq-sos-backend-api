using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.SosRequests;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetMySosRequests;

public class GetMySosRequestsQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ILogger<GetMySosRequestsQueryHandler> logger
) : IRequestHandler<GetMySosRequestsQuery, GetMySosRequestsResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ILogger<GetMySosRequestsQueryHandler> _logger = logger;

    public async Task<GetMySosRequestsResponse> Handle(GetMySosRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetMySosRequestsQuery for UserId={userId}", request.UserId);

        var requests = await _sosRequestRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return new GetMySosRequestsResponse
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