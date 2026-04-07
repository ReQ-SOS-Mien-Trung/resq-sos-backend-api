using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class GetSosRequestQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IUserRepository userRepository,
    ILogger<GetSosRequestQueryHandler> logger
) : IRequestHandler<GetSosRequestQuery, GetSosRequestResponse>
{
    private const int COORDINATOR_ROLE_ID = 2;
    private const int VICTIM_ROLE_ID = 5;

    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILogger<GetSosRequestQueryHandler> _logger = logger;

    public async Task<GetSosRequestResponse> Handle(GetSosRequestQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSosRequestQuery Id={id} RoleId={roleId}", request.Id, request.RequestingRoleId);

        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sosRequest is null)
            throw new NotFoundException("Không tìm thấy yêu cầu SOS");

        // Victim access: must be owner OR companion
        if (request.RequestingRoleId == VICTIM_ROLE_ID && sosRequest.UserId != request.RequestingUserId)
        {
            var isCompanion = await _companionRepository.IsCompanionAsync(request.Id, request.RequestingUserId, cancellationToken);
            if (!isCompanion)
                throw new ForbiddenException("Bạn không có quyền xem SOS request này");
        }

        if (request.RequestingRoleId != COORDINATOR_ROLE_ID && request.RequestingRoleId != VICTIM_ROLE_ID)
            throw new ForbiddenException("Bạn không có quyền truy cập");

        var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync([sosRequest.Id], cancellationToken);
        victimUpdateLookup.TryGetValue(sosRequest.Id, out var latestVictimUpdate);
        var effectiveSosRequest = SosRequestVictimUpdateOverlay.Apply(sosRequest, latestVictimUpdate);

        var incidentLookup = await _sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync([sosRequest.Id], cancellationToken);
        incidentLookup.TryGetValue(sosRequest.Id, out var incidents);
        var latestIncident = incidents?.FirstOrDefault();

        // Load companion list
        var companionRecords = await _companionRepository.GetBySosRequestIdAsync(request.Id, cancellationToken);
        List<CompanionResultDto>? companions = null;
        if (companionRecords.Count > 0)
        {
            var userIds = companionRecords.Select(c => c.UserId).ToList();
            var users = await _userRepository.GetByIdsAsync(userIds, cancellationToken);
            var userMap = users.ToDictionary(u => u.Id);

            companions = companionRecords.Select(c =>
            {
                userMap.TryGetValue(c.UserId, out var u);
                return new CompanionResultDto
                {
                    UserId = c.UserId,
                    FullName = u != null ? $"{u.LastName} {u.FirstName}".Trim() : null,
                    Phone = c.PhoneNumber ?? u?.Phone,
                    AddedAt = c.AddedAt
                };
            }).ToList();
        }

        return new GetSosRequestResponse
        {
            SosRequest = new SosRequestDetailDto
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
                IncidentHistory = incidents?.Select(x => new SosIncidentNoteDto
                {
                    Id = x.Id,
                    TeamIncidentId = x.TeamIncidentId,
                    MissionId = x.MissionId,
                    MissionTeamId = x.MissionTeamId,
                    MissionActivityId = x.MissionActivityId,
                    IncidentScope = x.IncidentScope,
                    Note = x.Note,
                    ReportedById = x.ReportedById,
                    CreatedAt = x.CreatedAt,
                    TeamName = x.TeamName,
                    ActivityType = x.ActivityType
                }).ToList(),
                Companions = companions
            }
        };
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}