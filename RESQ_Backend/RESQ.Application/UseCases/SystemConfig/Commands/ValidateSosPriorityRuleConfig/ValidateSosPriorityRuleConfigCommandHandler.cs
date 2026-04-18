using System.Text.Json;
using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ValidateSosPriorityRuleConfig;

public class ValidateSosPriorityRuleConfigCommandHandler(
    ISosRequestRepository sosRequestRepository,
    ISosPriorityEvaluationService priorityEvaluationService)
    : IRequestHandler<ValidateSosPriorityRuleConfigCommand, SosPriorityRuleConfigValidationResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosPriorityEvaluationService _priorityEvaluationService = priorityEvaluationService;

    public async Task<SosPriorityRuleConfigValidationResponse> Handle(ValidateSosPriorityRuleConfigCommand request, CancellationToken cancellationToken)
    {
        var errors = SosPriorityRuleConfigSupport.GetValidationErrors(request.Config).ToList();
        SosPriorityRuleConfigPreviewResponse? preview = null;

        if (errors.Count == 0 && request.SosRequestId.HasValue)
        {
            var sosRequest = await _sosRequestRepository.GetByIdAsync(request.SosRequestId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy SOS request với Id={request.SosRequestId.Value}.");

            var previewConfigModel = new SosPriorityRuleConfigModel
            {
                ConfigVersion = request.Config.ConfigVersion,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            SosPriorityRuleConfigSupport.SyncLegacyFields(previewConfigModel, request.Config);

            try
            {
                var evaluation = await _priorityEvaluationService.EvaluateWithConfigAsync(
                    sosRequest.Id,
                    sosRequest.StructuredData,
                    sosRequest.SosType,
                    previewConfigModel,
                    cancellationToken);

                var breakdown = ParseJson<SosPriorityEvaluationDetails>(evaluation.BreakdownJson ?? evaluation.DetailsJson);
                preview = new SosPriorityRuleConfigPreviewResponse
                {
                    SosRequestId = sosRequest.Id,
                    ConfigVersion = evaluation.ConfigVersion ?? request.Config.ConfigVersion,
                    PriorityScore = evaluation.TotalScore,
                    PriorityLevel = evaluation.PriorityLevel.ToString(),
                    Breakdown = breakdown
                };
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
        }

        return new SosPriorityRuleConfigValidationResponse
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Preview = errors.Count == 0 ? preview : null
        };
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}
