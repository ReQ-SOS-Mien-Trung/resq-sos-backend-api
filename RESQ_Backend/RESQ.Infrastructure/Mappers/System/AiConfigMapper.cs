using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Application.Services.Ai;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class AiConfigMapper
{
    public static AiConfig ToEntity(AiConfigModel model)
    {
        var normalizedApiUrl = AiProviderDefaults.ResolveApiUrl(model.Provider);

        var entity = new AiConfig
        {
            Name = model.Name,
            Provider = model.Provider.ToString(),
            Model = model.Model,
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            ApiUrl = normalizedApiUrl,
            ApiKey = model.ApiKey,
            Version = model.Version,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };

        if (model.Id > 0)
        {
            entity.Id = model.Id;
        }

        return entity;
    }

    public static AiConfigModel ToDomain(AiConfig entity)
    {
        var provider = Enum.TryParse<AiProvider>(entity.Provider, true, out var parsedProvider)
            ? parsedProvider
            : AiProvider.Gemini;

        return new AiConfigModel
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Provider = provider,
            Model = entity.Model ?? string.Empty,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            ApiUrl = AiProviderDefaults.ResolveApiUrl(provider),
            ApiKey = entity.ApiKey,
            Version = entity.Version,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
