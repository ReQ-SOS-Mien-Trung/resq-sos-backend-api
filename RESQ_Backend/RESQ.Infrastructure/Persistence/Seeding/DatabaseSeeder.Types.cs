using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private sealed class ConsumableInventoryHistoryPlan
    {
        public required SupplyInventory Inventory { get; init; }
        public required ItemModel ItemModel { get; init; }
        public required SupplyInventoryLot BaseLot { get; init; }
        public List<SupplyInventoryLot> SupplementalImportLots { get; init; } = [];
        public required Guid PerformedBy { get; init; }
        public int FinalQuantity => Inventory.Quantity ?? 0;
        public List<ConsumableOutboundEvent> OutboundEvents { get; } = [];
        public List<ConsumableInboundTransferEvent> InboundTransfers { get; } = [];
        public List<ConsumableAdjustmentEvent> Adjustments { get; } = [];
    }

    private sealed class ConsumableOutboundEvent
    {
        public required string ActionType { get; init; }
        public required string SourceType { get; init; }
        public int? SourceId { get; init; }
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public int? MissionId { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableInboundTransferEvent
    {
        public required int Quantity { get; init; }
        public int? SourceId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public DateTime? ReceivedDate { get; init; }
        public DateTime? ExpiredDate { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableAdjustmentEvent
    {
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public required string Note { get; init; }
    }

    private sealed record SeedArea(string Code, string Province, string Ward, string Address, double Lat, double Lon);
    private sealed record SeedCoordinate(double Lat, double Lon);

    private sealed record RelativeProfileSeed(
        string DisplayName,
        string PhoneNumber,
        string PersonType,
        string Gender,
        IReadOnlyList<string> Tags,
        string? MedicalBaselineNote,
        string? SpecialNeedsNote,
        string? SpecialDietNote,
        string MedicalProfileJson);

    private sealed record HueStadiumClusterScenario(
        string Code,
        double Latitude,
        double Longitude,
        double RadiusKm,
        string SeverityLevel,
        string WaterLevel,
        int VictimEstimated,
        int ChildrenCount,
        int ElderlyCount,
        double MedicalUrgencyScore,
        string Status,
        DateTime LocalCreatedAt);

    private sealed record HueStadiumSosScenario(
        int ClusterIndex,
        double Latitude,
        double Longitude,
        string Address,
        string SosType,
        string Status,
        string PriorityLevel,
        double PriorityScore,
        string Situation,
        bool CanMove,
        bool HasInjured,
        bool NeedMedical,
        bool OthersAreStable,
        string AdditionalDescription,
        string RawMessage,
        string Network,
        int BatteryPercentage,
        bool IsSentOnBehalf,
        int VictimIndex,
        int ReporterIndex,
        int CoordinatorIndex,
        DateTime LocalCreatedAt,
        IReadOnlyList<HueStadiumVictimScenario> Victims,
        IReadOnlyList<string> GroupNeeds);

    private sealed record HueStadiumSeedAiAnalysis(
        string Priority,
        string SeverityLevel,
        double Score,
        bool AgreesWithRuleBase,
        bool NeedsImmediateSafeTransfer,
        bool CanWaitForCombinedMission,
        string HandlingReason,
        string Explanation,
        IReadOnlyList<string> RuleConfigBasis);

    private sealed record HueStadiumVictimScenario(
        string PersonId,
        string PersonType,
        string CustomName,
        string IncidentStatus,
        IReadOnlyList<string> PersonalNeeds);

    private sealed record ItemTemplate(
        string CategoryCode,
        string Name,
        string Description,
        string Unit,
        string ItemType,
        decimal Volume,
        decimal Weight);
}
