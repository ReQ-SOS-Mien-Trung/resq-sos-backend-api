using RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class RemoveSosRequestFromClusterCommandValidatorTests
{
    private readonly RemoveSosRequestFromClusterCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenIdsAreInvalid()
    {
        var result = _validator.Validate(new RemoveSosRequestFromClusterCommand(0, 0, Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RemoveSosRequestFromClusterCommand.ClusterId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RemoveSosRequestFromClusterCommand.SosRequestId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RemoveSosRequestFromClusterCommand.RequestedByUserId));
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsWellFormed()
    {
        var result = _validator.Validate(
            new RemoveSosRequestFromClusterCommand(7, 101, Guid.NewGuid()));

        Assert.True(result.IsValid);
    }
}
