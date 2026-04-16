using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Security;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAllPrompts;

public class GetAllPromptsQueryHandler(
    IPromptRepository promptRepository,
    ILogger<GetAllPromptsQueryHandler> logger) : IRequestHandler<GetAllPromptsQuery, GetAllPromptsResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly ILogger<GetAllPromptsQueryHandler> _logger = logger;

    public async Task<GetAllPromptsResponse> Handle(GetAllPromptsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all prompts page={Page}", request.PageNumber);

        var pagedResult = await _promptRepository.GetAllPagedAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtos = pagedResult.Items.Select(p => new PromptDto
        {
            Id = p.Id,
            Status = PromptLifecycleStatusResolver.DetermineStatus(p),
            Name = p.Name,
            PromptType = p.PromptType,
            Provider = p.Provider,
            Purpose = p.Purpose,
            Model = p.Model,
            Temperature = p.Temperature,
            MaxTokens = p.MaxTokens,
            Version = p.Version,
            ApiUrl = p.ApiUrl,
            ApiKey = null,
            ApiKeyMasked = SecretMasker.Mask(p.ApiKey),
            HasApiKey = !string.IsNullOrWhiteSpace(p.ApiKey),
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        return new GetAllPromptsResponse
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
