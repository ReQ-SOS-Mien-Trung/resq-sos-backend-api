using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Emergency.Exceptions;
using RESQ.Domain.Enum.Emergency;

using RESQ.Application.Common;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Exceptions;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestCommandHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRuleEvaluationRepository sosRuleEvaluationRepository,
    ISosPriorityEvaluationService priorityEvaluationService,
    ISosAiAnalysisQueue aiAnalysisQueue,
    IUserRepository userRepository,
    ISosRequestCompanionRepository companionRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    ISosRequestRealtimeHubService sosRequestRealtimeHubService,
    ILogger<CreateSosRequestCommandHandler> logger
) : IRequestHandler<CreateSosRequestCommand, CreateSosRequestResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRuleEvaluationRepository _sosRuleEvaluationRepository = sosRuleEvaluationRepository;
    private readonly ISosPriorityEvaluationService _priorityEvaluationService = priorityEvaluationService;
    private readonly ISosAiAnalysisQueue _aiAnalysisQueue = aiAnalysisQueue;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly ISosRequestRealtimeHubService _sosRequestRealtimeHubService = sosRequestRealtimeHubService;
    private readonly ILogger<CreateSosRequestCommandHandler> _logger = logger;

    public async Task<CreateSosRequestResponse> Handle(CreateSosRequestCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateSosRequestCommand for UserId={userId}", request.UserId);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Người gửi (User)", request.UserId);
        }

        // Create SOS request with enum status
        var sosRequest = SosRequestModel.Create(
            request.UserId,
            request.Location,
            request.RawMessage,
            request.PacketId,
            request.OriginId,
            request.LocationAccuracy,
            request.SosType,
            request.StructuredData,
            request.NetworkMetadata,
            request.SenderInfo,
            request.Timestamp,
            SosRequestStatus.Pending,
            createdByCoordinatorId: request.CreatedByCoordinatorId,
            clientCreatedAt: request.ClientCreatedAt,
            victimInfo: request.VictimInfo,
            isSentOnBehalf: request.IsSentOnBehalf,
            reporterInfo: request.ReporterInfo);

        // Save SOS request first to get the ID
        await _sosRequestRepository.CreateAsync(sosRequest, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();

        if (succeedCount < 1)
            throw new SosRequestCreationFailedException();

        var created = await ResolveCreatedSosRequestAsync(sosRequest, cancellationToken);

        if (created is null)
            throw new SosRequestCreationFailedException();

        _logger.LogInformation("SOS Request created with Id={sosRequestId}, evaluating priority...", created.Id);

        // Evaluate priority based on structured data (rule-based, async)
        var evaluation = await _priorityEvaluationService.EvaluateAsync(
            created.Id,
            request.StructuredData,
            request.SosType,
            cancellationToken);

        // Save rule evaluation
        await _sosRuleEvaluationRepository.CreateAsync(evaluation, cancellationToken);

        // Update SOS request with priority level
        created.PriorityLevel = evaluation.PriorityLevel;
        created.PriorityScore = evaluation.TotalScore;
        await _sosRequestRepository.UpdateAsync(created, cancellationToken);

        var updateCount = await _unitOfWork.SaveAsync();
        if (updateCount < 1)
        {
            _logger.LogWarning("Failed to save rule evaluation for SOS Request Id={sosRequestId}", created.Id);
        }

        _logger.LogInformation(
            "SOS Request Id={sosRequestId} evaluated: TotalScore={totalScore}, Priority={priority}",
            created.Id, evaluation.TotalScore, evaluation.PriorityLevel);

        // Queue AI analysis to run in background (non-blocking)
        // Use created.* (from DB) instead of request.* so the fingerprint
        // matches what SosAiAnalysisService will compute from the DB later.
        await _aiAnalysisQueue.QueueAsync(SosAiAnalysisTask.Create(
            created.Id,
            created.StructuredData,
            created.RawMessage,
            created.SosType,
            evaluation));

        _logger.LogInformation("Queued AI analysis task for SOS Request Id={sosRequestId}", created.Id);

        // -- Companion linking: extract person_phone from structured_data.victims --
        var linkedCompanions = new List<CompanionLinkedResult>();
        try
        {
            var phones = ExtractVictimPhones(request.StructuredData);
            _logger.LogInformation("SOS #{SosId}: Extracted {Count} victim phones: [{Phones}]",
                created.Id, phones.Count, string.Join(", ", phones));

            if (phones.Count > 0)
            {
                foreach (var phone in phones)
                {
                    // Try both formats: original and +84 normalized
                    var foundUser = await _userRepository.GetByPhoneAsync(phone, cancellationToken);
                    if (foundUser is null && phone.StartsWith("0"))
                    {
                        foundUser = await _userRepository.GetByPhoneAsync("+84" + phone[1..], cancellationToken);
                    }
                    else if (foundUser is null && phone.StartsWith("+84"))
                    {
                        foundUser = await _userRepository.GetByPhoneAsync("0" + phone[3..], cancellationToken);
                    }

                    if (foundUser is null)
                    {
                        _logger.LogWarning("SOS #{SosId}: No user found for phone {Phone}", created.Id, phone);
                        continue;
                    }

                    if (foundUser.Id == request.UserId)
                    {
                        _logger.LogInformation("SOS #{SosId}: Skipping phone {Phone} — same as reporter", created.Id, phone);
                        continue;
                    }

                    // Avoid duplicates
                    if (linkedCompanions.Any(c => c.UserId == foundUser.Id))
                        continue;

                    _logger.LogInformation("SOS #{SosId}: Matched phone {Phone} to user {UserId}",
                        created.Id, phone, foundUser.Id);

                    linkedCompanions.Add(new CompanionLinkedResult
                    {
                        UserId = foundUser.Id,
                        FullName = $"{foundUser.LastName} {foundUser.FirstName}".Trim(),
                        Phone = foundUser.Phone ?? phone,
                    });
                }

                if (linkedCompanions.Count > 0)
                {
                    var records = linkedCompanions.Select(c => new SosRequestCompanionRecord(
                        0, created.Id, c.UserId, c.Phone, DateTime.UtcNow
                    ));
                    await _companionRepository.CreateRangeAsync(records, cancellationToken);
                    await _unitOfWork.SaveAsync();

                    _logger.LogInformation("Linked {count} companions to SOS Request Id={sosRequestId}",
                        linkedCompanions.Count, created.Id);

                    // Send notifications to companions (fire-and-forget)
                    foreach (var companion in linkedCompanions)
                    {
                        _ = _firebaseService.SendNotificationToUserAsync(
                            companion.UserId,
                            "Yêu cầu SOS cứu hộ",
                            $"Bạn đã được thêm vào yêu cầu SOS #{created.Id}. Nhấn để theo dõi tình hình.",
                            "sos_companion",
                            CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to link companions for SOS Request Id={sosRequestId}", created.Id);
        }

        await _sosRequestRealtimeHubService.PushSosRequestUpdateAsync(
            created.Id,
            "Created",
            cancellationToken: cancellationToken);
        // Push updated dashboard chart data to all connected admin clients (fire-and-forget)
        // Must be after companion linking to avoid DbContext concurrency
        _ = _dashboardHubService.PushVictimsByPeriodAsync(CancellationToken.None);

        return new CreateSosRequestResponse
        {
            Id = created.Id,
            PacketId = created.PacketId,
            ClusterId = created.ClusterId,
            OriginId = created.OriginId,
            UserId = created.UserId,
            SosType = created.SosType,
            RawMessage = created.RawMessage,
            StructuredData = SosStructuredDataParser.Parse(created.StructuredData),
            NetworkMetadata = ParseJson<SosNetworkMetadataDto>(created.NetworkMetadata),
            SenderInfo = ParseJson<SosSenderInfoDto>(created.SenderInfo),
            ReporterInfo = SosStructuredDataParser.ParseReporterInfo(created.ReporterInfo, created.SenderInfo),
            VictimInfo = ParseJson<SosVictimInfoDto>(created.VictimInfo),
            IsSentOnBehalf = created.IsSentOnBehalf,
            Status = created.Status.ToString(),
            PriorityLevel = evaluation.PriorityLevel.ToString(),
            Latitude = created.Location?.Latitude,
            Longitude = created.Location?.Longitude,
            LocationAccuracy = created.LocationAccuracy,
            Timestamp = created.Timestamp,
            CreatedAt = created.CreatedAt,
            ReceivedAt = created.ReceivedAt,
            LastUpdatedAt = created.LastUpdatedAt,
            ReviewedAt = created.ReviewedAt,
            ReviewedById = created.ReviewedById,
            CreatedByCoordinatorId = created.CreatedByCoordinatorId,
            LinkedCompanions = linkedCompanions.Count > 0 ? linkedCompanions : null
        };
    }

    /// <summary>
    /// Extract all non-empty person_phone values from structured_data.victims array.
    /// </summary>
    private static List<string> ExtractVictimPhones(string? structuredDataJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(structuredDataJson)) return result;

        try
        {
            using var doc = JsonDocument.Parse(structuredDataJson);
            if (doc.RootElement.TryGetProperty("victims", out var victims) && victims.ValueKind == JsonValueKind.Array)
            {
                foreach (var victim in victims.EnumerateArray())
                {
                    if (victim.TryGetProperty("person_phone", out var phoneProp) &&
                        phoneProp.ValueKind == JsonValueKind.String)
                    {
                        var phone = phoneProp.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(phone) && !result.Contains(phone))
                            result.Add(phone);
                    }
                }
            }
        }
        catch { /* malformed JSON — skip */ }

        return result;
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    private async Task<SosRequestModel?> ResolveCreatedSosRequestAsync(
        SosRequestModel createdRequest,
        CancellationToken cancellationToken)
    {
        if (createdRequest.Id > 0)
        {
            var createdById = await _sosRequestRepository.GetByIdAsync(createdRequest.Id, cancellationToken);
            if (createdById is not null)
                return createdById;
        }

        var createdAtUtc = createdRequest.CreatedAt?.ToUniversalTime();

        return (await _sosRequestRepository.GetByUserIdAsync(createdRequest.UserId, cancellationToken))
            .Where(x => string.Equals(x.RawMessage, createdRequest.RawMessage, StringComparison.Ordinal))
            .Where(x => createdRequest.PacketId is null || x.PacketId == createdRequest.PacketId)
            .Where(x => string.IsNullOrWhiteSpace(createdRequest.OriginId)
                || string.Equals(x.OriginId, createdRequest.OriginId, StringComparison.Ordinal))
            .Where(x => createdRequest.Timestamp is null || x.Timestamp == createdRequest.Timestamp)
            .Where(x => !createdAtUtc.HasValue
                || (x.CreatedAt.HasValue
                    && Math.Abs((x.CreatedAt.Value.ToUniversalTime() - createdAtUtc.Value).TotalSeconds) < 5))
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
    }
}
