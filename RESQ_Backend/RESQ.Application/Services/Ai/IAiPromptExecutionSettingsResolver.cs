using RESQ.Domain.Entities.System;

namespace RESQ.Application.Services.Ai;

public interface IAiPromptExecutionSettingsResolver
{
    AiPromptExecutionSettings Resolve(AiConfigModel aiConfig);
}
