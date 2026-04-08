using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Security;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptById;

public class GetPromptByIdQueryHandler(
    IPromptRepository promptRepository,
    ILogger<GetPromptByIdQueryHandler> logger) : IRequestHandler<GetPromptByIdQuery, GetPromptByIdResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly ILogger<GetPromptByIdQueryHandler> _logger = logger;

    public async Task<GetPromptByIdResponse> Handle(GetPromptByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting prompt by Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        return new GetPromptByIdResponse
        {
            Id = prompt.Id,
            Name = prompt.Name,
            PromptType = prompt.PromptType,
            Provider = prompt.Provider,
            Purpose = prompt.Purpose,
            SystemPrompt = prompt.SystemPrompt,
            UserPromptTemplate = prompt.UserPromptTemplate,
            Model = prompt.Model,
            Temperature = prompt.Temperature,
            MaxTokens = prompt.MaxTokens,
            Version = prompt.Version,
            ApiUrl = prompt.ApiUrl,
            ApiKey = null,
            ApiKeyMasked = SecretMasker.Mask(prompt.ApiKey),
            HasApiKey = !string.IsNullOrWhiteSpace(prompt.ApiKey),
            IsActive = prompt.IsActive,
            CreatedAt = prompt.CreatedAt,
            UpdatedAt = prompt.UpdatedAt
        };
    }
}
