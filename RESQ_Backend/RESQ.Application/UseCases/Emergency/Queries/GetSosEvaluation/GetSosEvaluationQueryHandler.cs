using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

public class GetSosEvaluationQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRuleEvaluationRepository sosRuleEvaluationRepository,
    ISosAiAnalysisRepository sosAiAnalysisRepository,
    ILogger<GetSosEvaluationQueryHandler> logger
) : IRequestHandler<GetSosEvaluationQuery, GetSosEvaluationResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly ISosRuleEvaluationRepository _ruleEvaluationRepository = sosRuleEvaluationRepository;
    private readonly ISosAiAnalysisRepository _aiAnalysisRepository = sosAiAnalysisRepository;
    private readonly ILogger<GetSosEvaluationQueryHandler> _logger = logger;

    public async Task<GetSosEvaluationResponse> Handle(GetSosEvaluationQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetSosEvaluationQuery SosRequestId={id} HasPrivilegedAccess={hasPrivilegedAccess}",
            request.SosRequestId, request.HasPrivilegedAccess);

        // 1. Kiểm tra SOS request tồn tại
        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken);
        if (sosRequest is null)
            throw new NotFoundException("Không tìm thấy yêu cầu SOS");

        if (!request.HasPrivilegedAccess && sosRequest.UserId != request.RequestingUserId)
        {
            var isCompanion = await _companionRepository.IsCompanionAsync(request.SosRequestId, request.RequestingUserId, cancellationToken);
            if (!isCompanion)
                throw new ForbiddenException("Bạn không có quyền xem đánh giá SOS request này.");
        }

        // 3. Lấy đánh giá rule-based và AI tuần tự (cùng DbContext instance, không thể song song)
        var ruleEvaluation = await _ruleEvaluationRepository.GetBySosRequestIdAsync(request.SosRequestId, cancellationToken);
        var aiAnalyses = (await _aiAnalysisRepository.GetAllBySosRequestIdAsync(request.SosRequestId, cancellationToken)).ToList();

        _logger.LogInformation(
            "GetSosEvaluationQuery SosRequestId={id} - RuleEvaluation={hasRule}, AiAnalyses={aiCount}",
            request.SosRequestId, ruleEvaluation is not null, aiAnalyses.Count);

        var evaluation = SosEvaluationViewFactory.CreateEvaluation(ruleEvaluation, aiAnalyses);

        return new GetSosEvaluationResponse
        {
            SosRequestId = sosRequest.Id,
            SosType = sosRequest.SosType,
            Status = sosRequest.Status.ToString(),
            CurrentPriorityLevel = sosRequest.PriorityLevel?.ToString(),
            Evaluation = evaluation
        };
    }
}
