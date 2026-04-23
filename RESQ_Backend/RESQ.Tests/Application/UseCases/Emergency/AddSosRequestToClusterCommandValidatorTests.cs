using RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class AddSosRequestToClusterCommandValidatorTests
{
    private readonly AddSosRequestToClusterCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenClusterIdIsInvalid(int clusterId)
    {
        var command = new AddSosRequestToClusterCommand(clusterId, 5, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddSosRequestToClusterCommand.ClusterId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenSosRequestIdIsInvalid(int sosRequestId)
    {
        var command = new AddSosRequestToClusterCommand(7, sosRequestId, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddSosRequestToClusterCommand.SosRequestId));
    }

    [Fact]
    public void Validate_Fails_WhenRequestedByUserIdIsEmpty()
    {
        var command = new AddSosRequestToClusterCommand(7, 5, Guid.Empty);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddSosRequestToClusterCommand.RequestedByUserId));
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var command = new AddSosRequestToClusterCommand(7, 5, Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
