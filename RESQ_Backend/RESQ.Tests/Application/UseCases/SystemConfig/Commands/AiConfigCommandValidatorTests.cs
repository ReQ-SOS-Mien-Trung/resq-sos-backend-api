using RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class AiConfigCommandValidatorTests
{
    [Fact]
    public void CreateAiConfigValidator_ShouldReturnClearProviderMessage_WhenProviderIsInvalid()
    {
        var validator = new CreateAiConfigCommandValidator();
        var command = new CreateAiConfigCommand(
            Name: "AI config test",
            Provider: (AiProvider)999,
            Model: "gemini-2.5-flash",
            Temperature: 0.5,
            MaxTokens: 256,
            ApiKey: "secret",
            Version: "v1",
            IsActive: true);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(CreateAiConfigCommand.Provider));
        Assert.Equal("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".", error.ErrorMessage);
    }

    [Fact]
    public void UpdateAiConfigValidator_ShouldReturnClearProviderMessage_WhenProviderIsInvalid()
    {
        var validator = new UpdateAiConfigCommandValidator();
        var command = new UpdateAiConfigCommand(
            Id: 1,
            Name: null,
            Provider: (AiProvider)999,
            Model: null,
            Temperature: null,
            MaxTokens: null,
            ApiKey: null,
            Version: null,
            IsActive: null);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(UpdateAiConfigCommand.Provider));
        Assert.Equal("Provider không hợp lệ. Giá trị hợp lệ: \"Gemini\" hoặc \"OpenRouter\".", error.ErrorMessage);
    }
}
