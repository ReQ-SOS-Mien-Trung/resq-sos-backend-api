using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Security;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigById;

public class GetAiConfigByIdQueryHandler(
    IAiConfigRepository aiConfigRepository,
    ILogger<GetAiConfigByIdQueryHandler> logger) : IRequestHandler<GetAiConfigByIdQuery, GetAiConfigByIdResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly ILogger<GetAiConfigByIdQueryHandler> _logger = logger;

    public async Task<GetAiConfigByIdResponse> Handle(GetAiConfigByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting AI config by Id={Id}", request.Id);

        var aiConfig = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken);
        if (aiConfig == null)
        {
            throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");
        }

        return new GetAiConfigByIdResponse
        {
            Id = aiConfig.Id,
            Status = PromptLifecycleStatusResolver.DetermineStatus(aiConfig),
            Name = aiConfig.Name,
            Provider = aiConfig.Provider,
            Model = aiConfig.Model,
            Temperature = aiConfig.Temperature,
            MaxTokens = aiConfig.MaxTokens,
            ApiUrl = aiConfig.ApiUrl,
            HasApiKey = !string.IsNullOrWhiteSpace(aiConfig.ApiKey),
            ApiKeyMasked = SecretMasker.Mask(aiConfig.ApiKey),
            Version = aiConfig.Version,
            IsActive = aiConfig.IsActive,
            CreatedAt = aiConfig.CreatedAt,
            UpdatedAt = aiConfig.UpdatedAt
        };
    }
}
