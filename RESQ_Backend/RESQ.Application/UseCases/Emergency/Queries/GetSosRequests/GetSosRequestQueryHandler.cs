using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class GetSosRequestQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ILogger<GetSosRequestQueryHandler> logger
) : IRequestHandler<GetSosRequestQuery, GetSosRequestResponse>
{
    private const int COORDINATOR_ROLE_ID = 2;
    private const int VICTIM_ROLE_ID = 5;

    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ILogger<GetSosRequestQueryHandler> _logger = logger;

    public async Task<GetSosRequestResponse> Handle(GetSosRequestQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSosRequestQuery Id={id} RoleId={roleId}", request.Id, request.RequestingRoleId);

        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sosRequest is null)
            throw new NotFoundException("Không tìm thấy yêu cầu SOS");

        if (request.RequestingRoleId == VICTIM_ROLE_ID && sosRequest.UserId != request.RequestingUserId)
            throw new ForbiddenException("Bạn không có quyền xem SOS request này");

        if (request.RequestingRoleId != COORDINATOR_ROLE_ID && request.RequestingRoleId != VICTIM_ROLE_ID)
            throw new ForbiddenException("Bạn không có quyền truy cập");

        return new GetSosRequestResponse
        {
            SosRequest = new SosRequestDetailDto
            {
                Id = sosRequest.Id,
                PacketId = sosRequest.PacketId,
                ClusterId = sosRequest.ClusterId,
                UserId = sosRequest.UserId,
                SosType = sosRequest.SosType,
                RawMessage = sosRequest.RawMessage,
                StructuredData = SosStructuredDataParser.Parse(sosRequest.StructuredData),
                NetworkMetadata = ParseJson<SosNetworkMetadataDto>(sosRequest.NetworkMetadata),
                SenderInfo = ParseJson<SosSenderInfoDto>(sosRequest.SenderInfo),
                ReporterInfo = SosStructuredDataParser.ParseReporterInfo(sosRequest.ReporterInfo, sosRequest.SenderInfo),
                VictimInfo = ParseJson<SosVictimInfoDto>(sosRequest.VictimInfo),
                IsSentOnBehalf = sosRequest.IsSentOnBehalf,
                OriginId = sosRequest.OriginId,
                Status = sosRequest.Status.ToString(),
                PriorityLevel = sosRequest.PriorityLevel?.ToString(),
                Latitude = sosRequest.Location?.Latitude,
                Longitude = sosRequest.Location?.Longitude,
                LocationAccuracy = sosRequest.LocationAccuracy,
                Timestamp = sosRequest.Timestamp,
                CreatedAt = sosRequest.CreatedAt,
                ReceivedAt = sosRequest.ReceivedAt,
                LastUpdatedAt = sosRequest.LastUpdatedAt,
                ReviewedAt = sosRequest.ReviewedAt,
                ReviewedById = sosRequest.ReviewedById,
                CreatedByCoordinatorId = sosRequest.CreatedByCoordinatorId
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