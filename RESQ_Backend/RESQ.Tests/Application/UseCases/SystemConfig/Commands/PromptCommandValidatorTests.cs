using RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Commands;

public class PromptCommandValidatorTests
{
    [Fact]
    public void CreatePromptValidator_ShouldRejectDraftVersionFormat()
    {
        var validator = new CreatePromptCommandValidator();
        var command = new CreatePromptCommand(
            Name: "Prompt test",
            PromptType: PromptType.MissionTeamPlanning,
            Purpose: "Test purpose",
            SystemPrompt: "System prompt",
            UserPromptTemplate: "User prompt",
            Version: "v1-D26041612",
            IsActive: true);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(CreatePromptCommand.Version));
        Assert.Equal("Version tạo mới không được dùng định dạng draft '-D'. Hãy dùng endpoint tạo draft.", error.ErrorMessage);
    }

    [Fact]
    public void CreatePromptValidator_ShouldAllowRegularVersionContainingDashDText()
    {
        var validator = new CreatePromptCommandValidator();
        var command = new CreatePromptCommand(
            Name: "Prompt test",
            PromptType: PromptType.MissionTeamPlanning,
            Purpose: "Test purpose",
            SystemPrompt: "System prompt",
            UserPromptTemplate: "User prompt",
            Version: "v1-DEMO",
            IsActive: true);

        var result = validator.Validate(command);

        Assert.DoesNotContain(result.Errors, x => x.PropertyName == nameof(CreatePromptCommand.Version));
    }

    [Fact]
    public void UpdatePromptValidator_ShouldRejectNonDraftVersionFormat()
    {
        var validator = new UpdatePromptCommandValidator();
        var command = new UpdatePromptCommand(
            Id: 1,
            Name: null,
            PromptType: null,
            Purpose: null,
            SystemPrompt: null,
            UserPromptTemplate: null,
            Version: "v1.1",
            IsActive: null);

        var result = validator.Validate(command);

        var error = Assert.Single(result.Errors, x => x.PropertyName == nameof(UpdatePromptCommand.Version));
        Assert.Equal("Version của draft phải chứa dấu hiệu '-D'.", error.ErrorMessage);
    }
}
