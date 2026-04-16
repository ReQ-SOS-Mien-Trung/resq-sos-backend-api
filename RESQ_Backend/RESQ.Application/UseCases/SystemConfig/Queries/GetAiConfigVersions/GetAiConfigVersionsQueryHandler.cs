using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Security;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.AiConfigs;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigVersions;

public class GetAiConfigVersionsQueryHandler(
    IAiConfigRepository aiConfigRepository,
    ILogger<GetAiConfigVersionsQueryHandler> logger) : IRequestHandler<GetAiConfigVersionsQuery, GetAiConfigVersionsResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly ILogger<GetAiConfigVersionsQueryHandler> _logger = logger;

    public async Task<GetAiConfigVersionsResponse> Handle(GetAiConfigVersionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting AI config versions by source Id={Id}", request.Id);

        var source = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken);
        if (source == null)
        {
            throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");
        }

        var versions = await _aiConfigRepository.GetVersionsAsync(cancellationToken);
        var orderedVersions = versions
            .OrderBy(config => GetStatusOrder(config))
            .ThenByDescending(config => config.UpdatedAt ?? config.CreatedAt)
            .ThenByDescending(config => config.Id)
            .ToList();

        return new GetAiConfigVersionsResponse
        {
            SourceAiConfigId = source.Id,
            Items = orderedVersions.Select(config => new AiConfigSummaryDto
            {
                Id = config.Id,
                Status = PromptLifecycleStatusResolver.DetermineStatus(config),
                Name = config.Name,
                Provider = config.Provider,
                Model = config.Model,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                ApiUrl = config.ApiUrl,
                HasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey),
                ApiKeyMasked = SecretMasker.Mask(config.ApiKey),
                Version = config.Version,
                IsActive = config.IsActive,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            }).ToList()
        };
    }

    private static int GetStatusOrder(RESQ.Domain.Entities.System.AiConfigModel config)
    {
        return PromptLifecycleStatusResolver.DetermineStatus(config) switch
        {
            "Active" => 0,
            "Draft" => 1,
            _ => 2
        };
    }
}
