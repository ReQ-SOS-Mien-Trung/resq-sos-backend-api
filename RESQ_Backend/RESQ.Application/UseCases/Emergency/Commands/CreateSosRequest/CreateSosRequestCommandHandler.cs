using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
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
    ILogger<CreateSosRequestCommandHandler> logger
) : IRequestHandler<CreateSosRequestCommand, CreateSosRequestResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRuleEvaluationRepository _sosRuleEvaluationRepository = sosRuleEvaluationRepository;
    private readonly ISosPriorityEvaluationService _priorityEvaluationService = priorityEvaluationService;
    private readonly ISosAiAnalysisQueue _aiAnalysisQueue = aiAnalysisQueue;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
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
            request.LocationAccuracy,
            request.SosType,
            request.StructuredData,
            request.NetworkMetadata,
            request.Timestamp,
            SosRequestStatus.Pending);

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

        // Evaluate priority based on structured data (rule-based, synchronous)
        var evaluation = _priorityEvaluationService.Evaluate(
            created.Id,
            request.StructuredData,
            request.SosType);

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

        return new CreateSosRequestResponse
        {
            Id = created.Id,
            PacketId = created.PacketId,
            UserId = created.UserId,
            SosType = created.SosType,
            RawMessage = created.RawMessage,
            StructuredData = created.StructuredData,
            NetworkMetadata = created.NetworkMetadata,
            Status = created.Status,
            PriorityLevel = evaluation.PriorityLevel,
            Latitude = created.Location?.Latitude,
            Longitude = created.Location?.Longitude,
            LocationAccuracy = created.LocationAccuracy,
            Timestamp = created.Timestamp,
            CreatedAt = created.CreatedAt
        };
    }
}