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
            PromptType = model.PromptType.ToString(),
            Purpose = model.Purpose,
            SystemPrompt = model.SystemPrompt,
            UserPromptTemplate = model.UserPromptTemplate,
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

    public static PromptModel ToDomain(Prompt entity)
    {
        return new PromptModel
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            PromptType = Enum.TryParse<RESQ.Domain.Enum.System.PromptType>(entity.PromptType, out var pt)
                ? pt
                : RESQ.Domain.Enum.System.PromptType.SosPriorityAnalysis,
            Purpose = entity.Purpose,
            SystemPrompt = entity.SystemPrompt,
            UserPromptTemplate = entity.UserPromptTemplate,
            Version = entity.Version,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
