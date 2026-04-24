namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class SeedDataOptions
{
    public string Profile { get; set; } = "Demo";
    public DateOnly AnchorDate { get; set; } = new(2026, 4, 24);
    public int RandomSeed { get; set; } = 20260424;
    public bool FailOnValidationError { get; set; } = true;

    public bool IsDemoProfile =>
        string.Equals(Profile, "Demo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Profile, "Full", StringComparison.OrdinalIgnoreCase);
}
