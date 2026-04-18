using RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Logistics.Commands;

/// <summary>
/// FE-05 – Depot Management: CreateSupplyRequest validator tests.
/// Covers: Coordinate Supply Pickup, Inter-Depot Supply Transfer Management.
/// </summary>
public class CreateSupplyRequestCommandValidatorTests
{
    private readonly CreateSupplyRequestCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenRequestsListIsEmpty()
    {
        var command = new CreateSupplyRequestCommand { Requests = [] };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateSupplyRequestCommand.Requests));
    }

    [Fact]
    public void Validate_Fails_WhenDuplicateSourceDepotIds()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests =
            [
                BuildGroup(sourceDepotId: 1),
                BuildGroup(sourceDepotId: 1)
            ]
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("kho nguồn"));
    }

    [Fact]
    public void Validate_Fails_WhenSourceDepotIdIsZero()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests = [BuildGroup(sourceDepotId: 0)]
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenItemsListIsEmpty()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests =
            [
                new SupplyRequestGroupDto
                {
                    SourceDepotId = 1,
                    PriorityLevel = SupplyRequestPriorityLevel.Medium,
                    Items = []
                }
            ]
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenItemQuantityIsZero()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests =
            [
                new SupplyRequestGroupDto
                {
                    SourceDepotId = 1,
                    PriorityLevel = SupplyRequestPriorityLevel.Medium,
                    Items = [new SupplyRequestItemDto { ItemModelId = 1, Quantity = 0 }]
                }
            ]
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenItemModelIdIsZero()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests =
            [
                new SupplyRequestGroupDto
                {
                    SourceDepotId = 1,
                    PriorityLevel = SupplyRequestPriorityLevel.Medium,
                    Items = [new SupplyRequestItemDto { ItemModelId = 0, Quantity = 10 }]
                }
            ]
        };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var command = new CreateSupplyRequestCommand
        {
            RequestingUserId = Guid.NewGuid(),
            Requests = [BuildGroup()]
        };

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WithMultipleDistinctDepots()
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests =
            [
                BuildGroup(sourceDepotId: 1),
                BuildGroup(sourceDepotId: 2)
            ]
        };

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(SupplyRequestPriorityLevel.Urgent)]
    [InlineData(SupplyRequestPriorityLevel.High)]
    [InlineData(SupplyRequestPriorityLevel.Medium)]
    public void Validate_Passes_WithValidPriorityLevels(SupplyRequestPriorityLevel priority)
    {
        var command = new CreateSupplyRequestCommand
        {
            Requests = [BuildGroup(priorityLevel: priority)]
        };

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    private static SupplyRequestGroupDto BuildGroup(
        int sourceDepotId = 1,
        SupplyRequestPriorityLevel priorityLevel = SupplyRequestPriorityLevel.Medium)
        => new()
        {
            SourceDepotId = sourceDepotId,
            PriorityLevel = priorityLevel,
            Items = [new SupplyRequestItemDto { ItemModelId = 1, Quantity = 10 }]
        };
}
