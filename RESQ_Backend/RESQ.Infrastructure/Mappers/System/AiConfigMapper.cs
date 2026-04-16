using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class AiConfigMapper
{
    public static AiConfig ToEntity(AiConfigModel model)
    {
        var entity = new AiConfig
        {
            Name = model.Name,
            Provider = model.Provider.ToString(),
            Model = model.Model,
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            ApiUrl = model.ApiUrl,
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
        return new AiConfigModel
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Provider = Enum.TryParse<AiProvider>(entity.Provider, true, out var provider)
                ? provider
                : AiProvider.Gemini,
            Model = entity.Model ?? string.Empty,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            ApiUrl = entity.ApiUrl ?? string.Empty,
            ApiKey = entity.ApiKey,
            Version = entity.Version,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
