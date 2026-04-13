using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;

namespace RESQ.Tests.Application.UseCases.Logistics;

/// <summary>
/// FE-05 – Depot Management: CreateDepot validator tests.
/// Covers: Track Warehouse/Depot, name/address/capacity input validation.
/// </summary>
public class CreateDepotCommandValidatorTests
{
    private readonly CreateDepotCommandValidator _validator = new();

    // ── Name validation ──

    [Fact]
    public void Validate_Fails_WhenNameIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(name: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Name));
    }

    [Fact]
    public void Validate_Fails_WhenNameExceeds200Characters()
    {
        var result = _validator.Validate(BuildCommand(name: new string('A', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Name));
    }

    // ── Address validation ──

    [Fact]
    public void Validate_Fails_WhenAddressIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(address: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Address));
    }

    [Fact]
    public void Validate_Fails_WhenAddressExceeds300Characters()
    {
        var result = _validator.Validate(BuildCommand(address: new string('B', 301)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Address));
    }

    // ── Coordinate validation (GPS) ──

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validate_Fails_WhenLatitudeIsOutOfRange(double lat)
    {
        var result = _validator.Validate(BuildCommand(latitude: lat));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Latitude));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Validate_Fails_WhenLongitudeIsOutOfRange(double lng)
    {
        var result = _validator.Validate(BuildCommand(longitude: lng));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Longitude));
    }

    // ── Capacity validation ──

    [Fact]
    public void Validate_Fails_WhenCapacityIsZero()
    {
        var result = _validator.Validate(BuildCommand(capacity: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Capacity));
    }

    [Fact]
    public void Validate_Fails_WhenCapacityIsNegative()
    {
        var result = _validator.Validate(BuildCommand(capacity: -10));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.Capacity));
    }

    [Fact]
    public void Validate_Fails_WhenWeightCapacityIsZero()
    {
        var result = _validator.Validate(BuildCommand(weightCapacity: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDepotCommand.WeightCapacity));
    }

    // ── Valid command ──

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.Validate(BuildCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WithBoundaryCoordinates()
    {
        var result = _validator.Validate(BuildCommand(latitude: -90, longitude: -180));
        Assert.True(result.IsValid);

        result = _validator.Validate(BuildCommand(latitude: 90, longitude: 180));
        Assert.True(result.IsValid);
    }

    // ── Builder ──

    private static CreateDepotCommand BuildCommand(
        string name = "Kho Đà Nẵng",
        string address = "123 Nguyễn Văn Linh, Đà Nẵng",
        double latitude = 16.047079,
        double longitude = 108.206230,
        decimal capacity = 5000,
        decimal weightCapacity = 2000)
        => new(name, address, latitude, longitude, capacity, weightCapacity);
}
