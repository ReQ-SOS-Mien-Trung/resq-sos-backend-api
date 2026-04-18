using RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

namespace RESQ.Tests.Application.UseCases.Emergency;

/// <summary>
/// FE-03 – Intelligent AI Dispatching: Validator tests for GenerateRescueMissionSuggestion.
/// Covers: ClusterId validation, UserId validation.
/// </summary>
public class GenerateRescueMissionSuggestionValidatorTests
{
    private readonly GenerateRescueMissionSuggestionValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenClusterIdIsZero()
    {
        var command = new GenerateRescueMissionSuggestionCommand(0, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GenerateRescueMissionSuggestionCommand.ClusterId));
    }

    [Fact]
    public void Validate_Fails_WhenClusterIdIsNegative()
    {
        var command = new GenerateRescueMissionSuggestionCommand(-5, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("ClusterId"));
    }

    [Fact]
    public void Validate_Fails_WhenUserIdIsEmpty()
    {
        var command = new GenerateRescueMissionSuggestionCommand(1, Guid.Empty);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GenerateRescueMissionSuggestionCommand.RequestedByUserId));
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var command = new GenerateRescueMissionSuggestionCommand(5, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenClusterIdIsOne()
    {
        var command = new GenerateRescueMissionSuggestionCommand(1, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
