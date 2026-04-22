namespace RESQ.Application.Common.Models;

public abstract class AdminRealtimeUpdateBase
{
    public int? EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Status { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AdminFundingRequestRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int RequestId { get; set; }
    public int DepotId { get; set; }
}

public sealed class AdminCampaignRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int CampaignId { get; set; }
}

public sealed class AdminDisbursementRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? DisbursementId { get; set; }
    public int? CampaignId { get; set; }
    public int DepotId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class AdminRescuerApplicationRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int ApplicationId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ReviewedBy { get; set; }
}

public sealed class AdminDepotRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? DepotId { get; set; }
}

public sealed class AdminDepotClosureRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? ClosureId { get; set; }
    public int? SourceDepotId { get; set; }
    public int? TargetDepotId { get; set; }
}

public sealed class AdminTransferRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int TransferId { get; set; }
    public int? ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public int TargetDepotId { get; set; }
}

public sealed class AdminSOSClusterRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? ClusterId { get; set; }
}

public sealed class AdminMissionRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int MissionId { get; set; }
    public int? ClusterId { get; set; }
}

public sealed class AdminMissionActivityRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int ActivityId { get; set; }
    public int? MissionId { get; set; }
    public int? DepotId { get; set; }
}

public sealed class AdminRescueTeamRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? TeamId { get; set; }
}

public sealed class AdminSystemConfigRealtimeUpdate : AdminRealtimeUpdateBase
{
    public string? ConfigKey { get; set; }
}

public sealed class AdminAiConfigRealtimeUpdate : AdminRealtimeUpdateBase
{
    public int? ConfigId { get; set; }
    public string? ConfigScope { get; set; }
}
