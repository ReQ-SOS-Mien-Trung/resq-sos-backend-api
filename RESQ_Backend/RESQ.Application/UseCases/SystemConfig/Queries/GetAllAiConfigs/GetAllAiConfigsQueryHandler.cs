using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Security;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Queries.AiConfigs;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllAiConfigs;

public class GetAllAiConfigsQueryHandler(
    IAiConfigRepository aiConfigRepository,
    ILogger<GetAllAiConfigsQueryHandler> logger) : IRequestHandler<GetAllAiConfigsQuery, GetAllAiConfigsResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly ILogger<GetAllAiConfigsQueryHandler> _logger = logger;

    public async Task<GetAllAiConfigsResponse> Handle(GetAllAiConfigsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all AI configs page={Page}", request.PageNumber);

        var pagedResult = await _aiConfigRepository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedResult.Items.Select(config => new AiConfigSummaryDto
        {
            Id = config.Id,
            Status = PromptLifecycleStatusResolver.DetermineStatus(config),
            Name = config.Name,
            Provider = config.Provider,
            Model = config.Model,
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens,
            ApiUrl = AiProviderDefaults.ResolveApiUrl(config.Provider),
            HasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey),
            ApiKeyMasked = SecretMasker.Mask(config.ApiKey),
            Version = config.Version,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        }).ToList();

        return new GetAllAiConfigsResponse
        {
            Items = dtos,
            PageNumber = pagedResult.PageNumber,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage
        };
    }
}
