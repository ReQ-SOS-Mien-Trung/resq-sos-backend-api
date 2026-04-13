using RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

namespace RESQ.Tests.Application.UseCases.Logistics.Commands;

/// <summary>
/// FE-05 – Depot Management: AdjustInventory validator tests.
/// Covers: Track Warehouse/Depot Inventory, inventory adjustment validation.
/// </summary>
public class AdjustInventoryCommandValidatorTests
{
    private readonly AdjustInventoryCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenItemModelIdIsZero()
    {
        var result = _validator.Validate(BuildCommand(itemModelId: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.ItemModelId));
    }

    [Fact]
    public void Validate_Fails_WhenItemModelIdIsNegative()
    {
        var result = _validator.Validate(BuildCommand(itemModelId: -1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.ItemModelId));
    }

    [Fact]
    public void Validate_Fails_WhenQuantityChangeIsZero()
    {
        var result = _validator.Validate(BuildCommand(quantityChange: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.QuantityChange));
    }

    [Fact]
    public void Validate_Fails_WhenReasonIsEmpty()
    {
        var result = _validator.Validate(BuildCommand(reason: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.Reason));
    }

    [Fact]
    public void Validate_Fails_WhenReasonExceeds500Characters()
    {
        var result = _validator.Validate(BuildCommand(reason: new string('R', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.Reason));
    }

    [Fact]
    public void Validate_Fails_WhenNoteExceeds500Characters()
    {
        var result = _validator.Validate(BuildCommand(note: new string('N', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustInventoryCommand.Note));
    }

    [Fact]
    public void Validate_Passes_WhenNoteIsNull()
    {
        var result = _validator.Validate(BuildCommand(note: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenPositiveQuantityChange()
    {
        var result = _validator.Validate(BuildCommand(quantityChange: 10));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenNegativeQuantityChange()
    {
        var result = _validator.Validate(BuildCommand(quantityChange: -5));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.Validate(BuildCommand());

        Assert.True(result.IsValid);
    }

    private static AdjustInventoryCommand BuildCommand(
        int itemModelId = 1,
        int quantityChange = 5,
        string reason = "Nhập bổ sung",
        string? note = "Hàng viện trợ đợt 2",
        DateTime? expiredDate = null)
        => new(Guid.NewGuid(), itemModelId, quantityChange, reason, note,
               expiredDate ?? DateTime.UtcNow.AddMonths(6));
}
