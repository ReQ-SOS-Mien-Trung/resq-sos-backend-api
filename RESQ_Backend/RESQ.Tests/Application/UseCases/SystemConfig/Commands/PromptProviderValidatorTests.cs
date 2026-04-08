using RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class PromptProviderValidatorTests
{
    [Fact]
    public void CreatePromptValidator_ShouldReturnClearProviderMessage_WhenProviderIsInvalid()
    {
        var validator = new CreatePromptCommandValidator();
        var command = new CreatePromptCommand(
            Name: "Prompt test",
            PromptType: PromptType.SosPriorityAnalysis,
            Provider: (AiProvider)999,
            Purpose: "Test purpose",
            SystemPrompt: "System prompt",
            UserPromptTemplate: "User prompt",
            Model: "gemini-2.5-flash",
            Temperature: 0.5,
            MaxTokens: 256,
            Version: "v1",
            ApiUrl: null,
            ApiKey: null,
            IsActive: true);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(CreatePromptCommand.Provider));
        Assert.Equal("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".", error.ErrorMessage);
    }

    [Fact]
    public void UpdatePromptValidator_ShouldReturnClearProviderMessage_WhenProviderIsInvalid()
    {
        var validator = new UpdatePromptCommandValidator();
        var command = new UpdatePromptCommand(
            Id: 1,
            Name: null,
            PromptType: null,
            Provider: (AiProvider)999,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            Version: null,
            ApiUrl: null,
            ApiKey: null,
            IsActive: null);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(UpdatePromptCommand.Provider));
        Assert.Equal("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".", error.ErrorMessage);
    }
}