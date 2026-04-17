using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class DemoSeedContext
{
    public required SeedDataOptions Options { get; init; }
    public required Random Random { get; init; }
    public required DateTime AnchorUtc { get; init; }
    public required DateTime StartUtc { get; init; }

    public List<User> Admins { get; } = [];
    public List<User> Coordinators { get; } = [];
    public List<User> Managers { get; } = [];
    public List<User> Rescuers { get; } = [];
    public List<User> Victims { get; } = [];

    public List<AssemblyPoint> AssemblyPoints { get; } = [];
    public List<Depot> Depots { get; } = [];
    public List<Category> Categories { get; } = [];
    public List<ItemModel> ItemModels { get; } = [];
    public List<SupplyInventory> Inventories { get; } = [];
    public List<SupplyInventoryLot> Lots { get; } = [];
    public List<ReusableItem> ReusableItems { get; } = [];
    public List<RescueTeam> RescueTeams { get; } = [];
    public List<RescueTeamMember> RescueTeamMembers { get; } = [];
    public List<SosCluster> SosClusters { get; } = [];
    public List<SosRequest> SosRequests { get; } = [];
    public List<Mission> Missions { get; } = [];
    public List<MissionTeam> MissionTeams { get; } = [];
    public List<MissionActivity> MissionActivities { get; } = [];
    public List<Conversation> Conversations { get; } = [];
    public List<DepotSupplyRequest> SupplyRequests { get; } = [];
    public List<FundCampaign> FundCampaigns { get; } = [];
    public List<FundingRequest> FundingRequests { get; } = [];
}
