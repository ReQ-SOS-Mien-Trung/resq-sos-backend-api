namespace RESQ.Application.Common.Models;

public sealed class SeedDataSyncReport
{
    public bool DryRun { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public SeedDataSyncSectionReport Campaigns { get; set; } = new("Campaigns");
    public SeedDataSyncSectionReport DepotFunds { get; set; } = new("DepotFunds");
    public SeedDataSyncSectionReport Inventory { get; set; } = new("Inventory");
    public SeedDataSyncSectionReport DerivedStates { get; set; } = new("DerivedStates");
    public List<string> Warnings { get; set; } = [];
    public List<int> AffectedCampaignIds { get; set; } = [];
    public List<int> AffectedDepotIds { get; set; } = [];
    public List<int> AffectedDepotFundIds { get; set; } = [];
    public List<int> AffectedDepotFundDepotIds { get; set; } = [];
    public bool HasChanges => Campaigns.Changed > 0
                              || DepotFunds.Changed > 0
                              || Inventory.Changed > 0
                              || DerivedStates.Changed > 0;
}

public sealed class SeedDataSyncSectionReport
{
    public SeedDataSyncSectionReport(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public int Scanned { get; set; }
    public int Changed { get; set; }
    public int Skipped { get; set; }
    public List<SeedDataSyncChange> Changes { get; set; } = [];
}

public sealed class SeedDataSyncChange
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
