using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class PromptMapper
{
    public static Prompt ToEntity(PromptModel model)
    {
        var entity = new Prompt
        {
            Name = model.Name,
            Purpose = model.Purpose,
            SystemPrompt = model.SystemPrompt,
            UserPromptTemplate = model.UserPromptTemplate,
            Model = model.Model,
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            Version = model.Version,
            CreatedAt = model.CreatedAt
        };

        if (model.Id > 0)
        {
            entity.Id = model.Id;
        }

        return entity;
    }

    public static PromptModel ToDomain(Prompt entity)
    {
        return new PromptModel
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Purpose = entity.Purpose,
            SystemPrompt = entity.SystemPrompt,
            UserPromptTemplate = entity.UserPromptTemplate,
            Model = entity.Model,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            Version = entity.Version,
            CreatedAt = entity.CreatedAt
        };
    }
}
