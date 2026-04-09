using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;

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

        // 4. Map rule evaluation
        SosRuleEvaluationDto? ruleDto = null;
        if (ruleEvaluation is not null)
        {
            var itemsNeeded = DeserializeItems(ruleEvaluation.ItemsNeeded);
            var breakdown = ParseJson<SosPriorityEvaluationDetails>(ruleEvaluation.BreakdownJson ?? ruleEvaluation.DetailsJson);
            ruleDto = new SosRuleEvaluationDto
            {
                Id = ruleEvaluation.Id,
                ConfigId = ruleEvaluation.ConfigId,
                ConfigVersion = ruleEvaluation.ConfigVersion,
                MedicalScore = ruleEvaluation.MedicalScore,
                InjuryScore = ruleEvaluation.InjuryScore,
                MobilityScore = ruleEvaluation.MobilityScore,
                EnvironmentScore = ruleEvaluation.EnvironmentScore,
                FoodScore = ruleEvaluation.FoodScore,
                TotalScore = ruleEvaluation.TotalScore,
                PriorityLevel = ruleEvaluation.PriorityLevel.ToString(),
                RuleVersion = ruleEvaluation.RuleVersion,
                ItemsNeeded = itemsNeeded,
                Breakdown = breakdown,
                CreatedAt = ruleEvaluation.CreatedAt
            };
        }

        // 5. Map AI analyses
        var aiDtos = aiAnalyses.Select(ai => new SosAiAnalysisDto
        {
            Id = ai.Id,
            ModelName = ai.ModelName,
            ModelVersion = ai.ModelVersion,
            AnalysisType = ai.AnalysisType,
            SuggestedSeverityLevel = ai.SuggestedSeverityLevel,
            SuggestedPriority = ai.SuggestedPriority,
            Explanation = ai.Explanation,
            ConfidenceScore = ai.ConfidenceScore,
            SuggestionScope = ai.SuggestionScope,
            Metadata = ParseJson(ai.Metadata),
            CreatedAt = ai.CreatedAt,
            AdoptedAt = ai.AdoptedAt
        }).ToList();

        return new GetSosEvaluationResponse
        {
            SosRequestId = sosRequest.Id,
            SosType = sosRequest.SosType,
            Status = sosRequest.Status.ToString(),
            CurrentPriorityLevel = sosRequest.PriorityLevel?.ToString(),
            RuleEvaluation = ruleDto,
            AiAnalyses = aiDtos
        };
    }

    private static List<string> DeserializeItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return null; }
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }
}
