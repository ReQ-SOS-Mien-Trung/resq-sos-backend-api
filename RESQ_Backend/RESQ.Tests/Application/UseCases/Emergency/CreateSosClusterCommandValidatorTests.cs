using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

namespace RESQ.Tests.Application.UseCases.Emergency;

public class CreateSosClusterCommandValidatorTests
{
    private readonly CreateSosClusterCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenSosRequestIdsIsEmpty()
    {
        var command = new CreateSosClusterCommand([], Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateSosClusterCommand.SosRequestIds));
    }

    [Fact]
    public void Validate_Fails_WhenSosRequestIdsContainsDuplicates()
    {
        var command = new CreateSosClusterCommand([1, 2, 2, 3], Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Danh sách SOS request không được có ID trùng lặp", error.ErrorMessage);
    }

    [Fact]
    public void Validate_Passes_WhenSosRequestIdsAreDistinct()
    {
        var command = new CreateSosClusterCommand([1, 2, 3], Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenSingleSosRequestId()
    {
        var command = new CreateSosClusterCommand([42], Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
