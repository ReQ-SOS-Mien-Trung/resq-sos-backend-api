using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;

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
                PacketId = x.PacketId,
                ClusterId = x.ClusterId,
                UserId = x.UserId,
                SosType = x.SosType,
                RawMessage = x.RawMessage,
                StructuredData = ParseJson(x.StructuredData),
                NetworkMetadata = ParseJson(x.NetworkMetadata),
                SenderInfo = ParseJson(x.SenderInfo),
                OriginId = x.OriginId,
                Status = x.Status.ToString(),
                PriorityLevel = x.PriorityLevel?.ToString(),
                WaitTimeMinutes = x.WaitTimeMinutes,
                Latitude = x.Location?.Latitude,
                Longitude = x.Location?.Longitude,
                LocationAccuracy = x.LocationAccuracy,
                Timestamp = x.Timestamp,
                CreatedAt = x.CreatedAt,
                LastUpdatedAt = x.LastUpdatedAt,
                ReviewedAt = x.ReviewedAt,
                ReviewedById = x.ReviewedById
            }).ToList()
        };
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return null; }
    }
}