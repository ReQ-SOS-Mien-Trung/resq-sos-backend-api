using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;

namespace RESQ.Infrastructure.Services.Ai;

public class AiPromptExecutionSettingsResolver : IAiPromptExecutionSettingsResolver
{
    public AiPromptExecutionSettings Resolve(AiConfigModel aiConfig)
    {
        return new AiPromptExecutionSettings(
            aiConfig.Provider,
            aiConfig.Model ?? string.Empty,
            AiProviderDefaults.ResolveApiUrl(aiConfig.Provider),
            aiConfig.ApiKey ?? string.Empty,
            aiConfig.Temperature,
            aiConfig.MaxTokens);
    }
}
