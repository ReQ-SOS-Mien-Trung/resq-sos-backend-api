using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Tests.Infrastructure.Mappers;

public class PromptMapperTests
{
    [Fact]
    public void ToDomain_ShouldDefaultProviderToGemini_WhenStoredProviderIsMissingOrInvalid()
    {
        var entity = new Prompt
        {
            Id = 99,
            Name = "Legacy Prompt",
            PromptType = "SosPriorityAnalysis",
            Provider = string.Empty
        };

        var domain = PromptMapper.ToDomain(entity);

        Assert.Equal(AiProvider.Gemini, domain.Provider);
    }
}