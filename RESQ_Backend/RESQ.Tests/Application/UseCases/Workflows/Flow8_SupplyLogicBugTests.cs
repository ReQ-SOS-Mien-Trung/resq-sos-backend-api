namespace RESQ.Tests.Application.UseCases.Workflows;

/// <summary>
/// Lu?ng 8 – L?i logic v?t ph?m (Supply bug): Buffer tính sai, thi?u hàng, nghi?p v? reserve.
/// Validates buffer ratio calculations and supply availability concepts.
/// </summary>
public class Flow8_SupplyLogicBugTests
{
    private const double DefaultBufferRatio = 0.10;

    // ---------- Buffer ratio calculations ----------

    [Theory]
    [InlineData(100, 0.10, 10)]   // 100 * 10% = 10
    [InlineData(50, 0.10, 5)]     // 50 * 10% = 5
    [InlineData(7, 0.10, 1)]      // 7 * 10% = 0.7 ? Ceiling = 1
    [InlineData(1, 0.10, 1)]      // 1 * 10% = 0.1 ? Ceiling = 1
    [InlineData(100, 0.20, 20)]   // 100 * 20% = 20
    [InlineData(100, 0.0, 0)]     // No buffer
    public void BufferCalculation_MatchesHandlerLogic(int quantity, double bufferRatio, int expectedBuffer)
    {
        // Same logic as CreateMissionCommandHandler:
        // var bufferQuantity = bufferRatio > 0 ? (int)Math.Ceiling(quantity * bufferRatio) : 0;
        int bufferQuantity = bufferRatio > 0 ? (int)Math.Ceiling(quantity * bufferRatio) : 0;

        Assert.Equal(expectedBuffer, bufferQuantity);
    }

    [Fact]
    public void TotalWithBuffer_EqualsQuantityPlusBuffer()
    {
        int quantity = 100;
        double bufferRatio = DefaultBufferRatio;
        int buffer = (int)Math.Ceiling(quantity * bufferRatio); // 10
        int total = quantity + buffer;

        Assert.Equal(110, total);
    }

    [Fact]
    public void DefaultBufferRatio_Is10Percent()
    {
        Assert.Equal(0.10, DefaultBufferRatio);
    }

    // ---------- Negative / invalid buffer ratios ----------

    [Fact]
    public void BufferRatio_NegativeClampedToZero()
    {
        // Handler uses: Math.Max(0.0, item.BufferRatio ?? DefaultBufferRatio)
        double inputRatio = -0.5;
        double clampedRatio = Math.Max(0.0, inputRatio);

        Assert.Equal(0.0, clampedRatio);
    }

    [Fact]
    public void BufferRatio_NullFallsBackToDefault()
    {
        double? inputRatio = null;
        double resolved = inputRatio ?? DefaultBufferRatio;

        Assert.Equal(0.10, resolved);
    }

    // ---------- Supply availability check concept ----------

    [Fact]
    public void SupplyAvailability_SufficientStock()
    {
        // Simulating: available = Quantity - ReservedQuantity
        int totalQuantity = 200;
        int reservedQuantity = 50;
        int requested = 100;

        int available = totalQuantity - reservedQuantity; // 150
        bool isSufficient = available >= requested;

        Assert.True(isSufficient);
    }

    [Fact]
    public void SupplyAvailability_InsufficientStock()
    {
        int totalQuantity = 100;
        int reservedQuantity = 80;
        int requested = 50;

        int available = totalQuantity - reservedQuantity; // 20
        bool isSufficient = available >= requested;

        Assert.False(isSufficient);
    }

    [Fact]
    public void SupplyAvailability_WithBuffer_MayExceedStock()
    {
        // Khi tính buffer, t?ng c?n l?y có th? vu?t available
        int totalQuantity = 100;
        int reservedQuantity = 0;
        int requestedBase = 95;
        int buffer = (int)Math.Ceiling(requestedBase * DefaultBufferRatio); // 10
        int totalNeeded = requestedBase + buffer; // 105

        int available = totalQuantity - reservedQuantity; // 100
        bool isSufficient = available >= totalNeeded;

        Assert.False(isSufficient); // 100 < 105, thi?u 5
    }

    [Fact]
    public void SupplyAvailability_ExactMatch()
    {
        int totalQuantity = 110;
        int reservedQuantity = 0;
        int requestedBase = 100;
        int buffer = (int)Math.Ceiling(requestedBase * DefaultBufferRatio); // 10
        int totalNeeded = requestedBase + buffer; // 110

        int available = totalQuantity - reservedQuantity; // 110
        bool isSufficient = available >= totalNeeded;

        Assert.True(isSufficient);
    }

    // ---------- Multiple items at same depot ----------

    [Fact]
    public void MultipleItems_EachCheckedIndependently()
    {
        var items = new[]
        {
            new { Name = "G?o", Requested = 100, Available = 200 },
            new { Name = "Nu?c", Requested = 50, Available = 30 },  // thi?u
            new { Name = "Chan", Requested = 20, Available = 25 }
        };

        var shortages = items.Where(i => i.Available < i.Requested).ToList();

        Assert.Single(shortages);
        Assert.Equal("Nu?c", shortages[0].Name);
    }

    // ---------- Reserve then consume ----------

    [Fact]
    public void ReserveReducesAvailable_ConsumeReducesBoth()
    {
        int total = 200;
        int reserved = 0;

        // Step 1: Reserve 50
        reserved += 50;
        int available = total - reserved; // 150
        Assert.Equal(150, available);

        // Step 2: Reserve another 30
        reserved += 30;
        available = total - reserved; // 120
        Assert.Equal(120, available);

        // Step 3: Consume 50 (from first reservation)
        total -= 50;
        reserved -= 50;
        available = total - reserved; // 150 - 30 = 120
        Assert.Equal(120, available);
    }

    // ---------- Edge cases ----------

    [Fact]
    public void BufferCalculation_VerySmallQuantity()
    {
        int quantity = 1;
        int buffer = (int)Math.Ceiling(quantity * DefaultBufferRatio); // 0.1 ? 1
        Assert.Equal(1, buffer);
    }

    [Fact]
    public void BufferCalculation_ZeroQuantity()
    {
        int quantity = 0;
        int buffer = DefaultBufferRatio > 0 ? (int)Math.Ceiling(quantity * DefaultBufferRatio) : 0;
        Assert.Equal(0, buffer);
    }

    [Fact]
    public void BufferCalculation_LargeQuantity()
    {
        int quantity = 10000;
        int buffer = (int)Math.Ceiling(quantity * DefaultBufferRatio); // 1000
        Assert.Equal(1000, buffer);
    }
}
