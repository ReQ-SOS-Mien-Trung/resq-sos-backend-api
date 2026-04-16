namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class SeedDataOptions
{
    public bool Enabled { get; set; }
    public string Profile { get; set; } = "Demo";
    public DateOnly AnchorDate { get; set; } = new(2026, 4, 16);
    public int RandomSeed { get; set; } = 20260416;
    public bool FailOnValidationError { get; set; } = true;

    public bool IsDemoProfile =>
        string.Equals(Profile, "Demo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Profile, "Full", StringComparison.OrdinalIgnoreCase);
}
