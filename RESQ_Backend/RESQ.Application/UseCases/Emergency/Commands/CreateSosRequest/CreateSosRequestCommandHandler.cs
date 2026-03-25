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

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestCommandHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRuleEvaluationRepository sosRuleEvaluationRepository,
    ISosPriorityEvaluationService priorityEvaluationService,
    ISosAiAnalysisQueue aiAnalysisQueue,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    ILogger<CreateSosRequestCommandHandler> logger
) : IRequestHandler<CreateSosRequestCommand, CreateSosRequestResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRuleEvaluationRepository _sosRuleEvaluationRepository = sosRuleEvaluationRepository;
    private readonly ISosPriorityEvaluationService _priorityEvaluationService = priorityEvaluationService;
    private readonly ISosAiAnalysisQueue _aiAnalysisQueue = aiAnalysisQueue;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly ILogger<CreateSosRequestCommandHandler> _logger = logger;

    public async Task<CreateSosRequestResponse> Handle(CreateSosRequestCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateSosRequestCommand for UserId={userId}", request.UserId);

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
            clientCreatedAt: request.ClientCreatedAt);

        // Save SOS request first to get the ID
        await _sosRequestRepository.CreateAsync(sosRequest, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();

        if (succeedCount < 1)
            throw new SosRequestCreationFailedException();

        // Get the created SOS request to retrieve its ID
        var created = (await _sosRequestRepository.GetByUserIdAsync(request.UserId, cancellationToken))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.RawMessage == request.RawMessage);

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
        await _aiAnalysisQueue.QueueAsync(new SosAiAnalysisTask(
            created.Id,
            request.StructuredData,
            request.RawMessage,
            request.SosType));

        _logger.LogInformation("Queued AI analysis task for SOS Request Id={sosRequestId}", created.Id);

        // Push updated dashboard chart data to all connected admin clients (fire-and-forget)
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
            StructuredData = ParseJson<SosStructuredDataDto>(created.StructuredData),
            NetworkMetadata = ParseJson<SosNetworkMetadataDto>(created.NetworkMetadata),
            SenderInfo = ParseJson<SosSenderInfoDto>(created.SenderInfo),
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
            CreatedByCoordinatorId = created.CreatedByCoordinatorId
        };
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}