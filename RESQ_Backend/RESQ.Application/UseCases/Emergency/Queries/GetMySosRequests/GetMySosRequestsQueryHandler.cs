using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;

public class GetMySosRequestsQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ILogger<GetMySosRequestsQueryHandler> logger
) : IRequestHandler<GetMySosRequestsQuery, GetMySosRequestsResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ILogger<GetMySosRequestsQueryHandler> _logger = logger;

    public async Task<GetMySosRequestsResponse> Handle(GetMySosRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetMySosRequestsQuery for UserId={userId}", request.UserId);

        var ownRequests = await _sosRequestRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var companionRequests = await _sosRequestRepository.GetByCompanionUserIdAsync(request.UserId, cancellationToken);
        var requestIds = ownRequests.Select(x => x.Id)
            .Concat(companionRequests.Select(x => x.Id))
            .Distinct()
            .ToList();
        var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(requestIds, cancellationToken);
        var incidentLookup = await _sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(requestIds, cancellationToken);

        var ownDtos = ownRequests.Select(x => MapToDto(x, isCompanion: false, incidentLookup, victimUpdateLookup));
        var companionDtos = companionRequests.Select(x => MapToDto(x, isCompanion: true, incidentLookup, victimUpdateLookup));

        // Merge own + companion, deduplicate by Id, order by CreatedAt desc
        var merged = ownDtos.Concat(companionDtos)
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        return new GetMySosRequestsResponse
        {
            SosRequests = merged
        };
    }

    private static SosRequestDto MapToDto(
        RESQ.Domain.Entities.Emergency.SosRequestModel x,
        bool isCompanion,
        IReadOnlyDictionary<int, IReadOnlyList<RESQ.Domain.Entities.Emergency.SosRequestIncidentUpdateModel>> incidentLookup,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Emergency.SosRequestVictimUpdateModel> victimUpdateLookup)
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
            LatestIncidentAt = latestIncident?.CreatedAt,
            IsCompanion = isCompanion
        };
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}
