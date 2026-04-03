using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;

public class GetMySosRequestsQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ILogger<GetMySosRequestsQueryHandler> logger
) : IRequestHandler<GetMySosRequestsQuery, GetMySosRequestsResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly ILogger<GetMySosRequestsQueryHandler> _logger = logger;

    public async Task<GetMySosRequestsResponse> Handle(GetMySosRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetMySosRequestsQuery for UserId={userId}", request.UserId);

        var ownRequests = await _sosRequestRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var companionRequests = await _sosRequestRepository.GetByCompanionUserIdAsync(request.UserId, cancellationToken);

        var ownDtos = ownRequests.Select(x => MapToDto(x, isCompanion: false));
        var companionDtos = companionRequests.Select(x => MapToDto(x, isCompanion: true));

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

    private static SosRequestDto MapToDto(RESQ.Domain.Entities.Emergency.SosRequestModel x, bool isCompanion)
    {
        return new SosRequestDto
        {
            Id = x.Id,
            PacketId = x.PacketId,
            ClusterId = x.ClusterId,
            UserId = x.UserId,
            SosType = x.SosType,
            RawMessage = x.RawMessage,
            StructuredData = SosStructuredDataParser.Parse(x.StructuredData),
            NetworkMetadata = ParseJson<SosNetworkMetadataDto>(x.NetworkMetadata),
            SenderInfo = ParseJson<SosSenderInfoDto>(x.SenderInfo),
            ReporterInfo = SosStructuredDataParser.ParseReporterInfo(x.ReporterInfo, x.SenderInfo),
            VictimInfo = ParseJson<SosVictimInfoDto>(x.VictimInfo),
            IsSentOnBehalf = x.IsSentOnBehalf,
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
            CreatedByCoordinatorId = x.CreatedByCoordinatorId,
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