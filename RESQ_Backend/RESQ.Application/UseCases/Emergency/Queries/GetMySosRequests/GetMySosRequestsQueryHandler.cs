using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;

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
                PacketId = x.PacketId,
                ClusterId = x.ClusterId,
                UserId = x.UserId,
                SosType = x.SosType,
                RawMessage = x.RawMessage,
                StructuredData = ParseJson<SosStructuredDataDto>(x.StructuredData),
                NetworkMetadata = ParseJson<SosNetworkMetadataDto>(x.NetworkMetadata),
                SenderInfo = ParseJson<SosSenderInfoDto>(x.SenderInfo),
                OriginId = x.OriginId,
                Status = x.Status.ToString(),
                PriorityLevel = x.PriorityLevel?.ToString(),
                Latitude = x.Location?.Latitude,
                Longitude = x.Location?.Longitude,
                LocationAccuracy = x.LocationAccuracy,
                Timestamp = x.Timestamp,
                CreatedAt = x.CreatedAt,
                ReceivedAt = x.ReceivedAt,
                LastUpdatedAt = x.LastUpdatedAt,
                ReviewedAt = x.ReviewedAt,
                ReviewedById = x.ReviewedById,
                CreatedByCoordinatorId = x.CreatedByCoordinatorId
            }).ToList()
        };
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}