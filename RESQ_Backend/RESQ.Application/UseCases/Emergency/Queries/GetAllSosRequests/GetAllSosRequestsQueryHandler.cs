using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;

public class GetAllSosRequestsQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ILogger<GetAllSosRequestsQueryHandler> logger
) : IRequestHandler<GetAllSosRequestsQuery, GetAllSosRequestsResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ILogger<GetAllSosRequestsQueryHandler> _logger = logger;

    public async Task<GetAllSosRequestsResponse> Handle(GetAllSosRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetAllSosRequestsQuery");

        var requests = await _sosRequestRepository.GetAllAsync(cancellationToken);
        var requestList = requests.ToList();
        var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            requestList.Select(x => x.Id),
            cancellationToken);
        var incidentLookup = await _sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(
            requestList.Select(x => x.Id),
            cancellationToken);

        return new GetAllSosRequestsResponse
        {
            SosRequests = requestList.Select(x =>
            {
                victimUpdateLookup.TryGetValue(x.Id, out var latestVictimUpdate);
                var effectiveSosRequest = SosRequestVictimUpdateOverlay.Apply(x, latestVictimUpdate);

                incidentLookup.TryGetValue(x.Id, out var incidents);
                var latestIncident = incidents?.FirstOrDefault();

                return new SosRequestDto
                {
                    Id = effectiveSosRequest.Id,
                    PacketId = effectiveSosRequest.PacketId,
                    ClusterId = effectiveSosRequest.ClusterId,
                    UserId = effectiveSosRequest.UserId,
                    SosType = effectiveSosRequest.SosType,
                    RawMessage = effectiveSosRequest.RawMessage,
                    StructuredData = SosStructuredDataParser.Parse(effectiveSosRequest.StructuredData),
                    NetworkMetadata = ParseJson<SosNetworkMetadataDto>(effectiveSosRequest.NetworkMetadata),
                    SenderInfo = ParseJson<SosSenderInfoDto>(effectiveSosRequest.SenderInfo),
                    ReporterInfo = SosStructuredDataParser.ParseReporterInfo(effectiveSosRequest.ReporterInfo, effectiveSosRequest.SenderInfo),
                    VictimInfo = ParseJson<SosVictimInfoDto>(effectiveSosRequest.VictimInfo),
                    IsSentOnBehalf = effectiveSosRequest.IsSentOnBehalf,
                    OriginId = effectiveSosRequest.OriginId,
                    Status = effectiveSosRequest.Status.ToString(),
                    PriorityLevel = effectiveSosRequest.PriorityLevel?.ToString(),
                    Latitude = effectiveSosRequest.Location?.Latitude,
                    Longitude = effectiveSosRequest.Location?.Longitude,
                    LocationAccuracy = effectiveSosRequest.LocationAccuracy,
                    Timestamp = effectiveSosRequest.Timestamp,
                    CreatedAt = effectiveSosRequest.CreatedAt,
                    ReceivedAt = effectiveSosRequest.ReceivedAt,
                    LastUpdatedAt = effectiveSosRequest.LastUpdatedAt,
                    ReviewedAt = effectiveSosRequest.ReviewedAt,
                    ReviewedById = effectiveSosRequest.ReviewedById,
                    CreatedByCoordinatorId = effectiveSosRequest.CreatedByCoordinatorId,
                    LatestIncidentNote = latestIncident?.Note,
                    LatestIncidentAt = latestIncident?.CreatedAt
                };
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