using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext : DbContext
{
    public ResQDbContext(DbContextOptions<ResQDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ability> Abilities { get; set; }
    public virtual DbSet<ActivityAiSuggestion> ActivityAiSuggestions { get; set; }
    public virtual DbSet<AssemblyPoint> AssemblyPoints { get; set; }
    public virtual DbSet<ClusterAiAnalysis> ClusterAiAnalyses { get; set; }
    public virtual DbSet<Conversation> Conversations { get; set; }
    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public virtual DbSet<Depot> Depots { get; set; }
    public virtual DbSet<DepotFundAllocation> DepotFundAllocations { get; set; }
    public virtual DbSet<DepotManager> DepotManagers { get; set; }
    public virtual DbSet<DepotSupplyInventory> DepotSupplyInventories { get; set; }
    public virtual DbSet<Donation> Donations { get; set; }
    public virtual DbSet<FundCampaign> FundCampaigns { get; set; }
    public virtual DbSet<FundTransaction> FundTransactions { get; set; }
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }
    public virtual DbSet<ItemCategory> ItemCategories { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<Mission> Missions { get; set; }
    public virtual DbSet<MissionActivity> MissionActivities { get; set; }
    public virtual DbSet<MissionAiSuggestion> MissionAiSuggestions { get; set; }
    public virtual DbSet<MissionItem> MissionItems { get; set; }
    public virtual DbSet<MissionTeam> MissionTeams { get; set; }
    public virtual DbSet<MissionTeamMember> MissionTeamMembers { get; set; }
    public virtual DbSet<MissionVehicle> MissionVehicles { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Organization> Organizations { get; set; }
    public virtual DbSet<OrganizationReliefItem> OrganizationReliefItems { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Prompt> Prompts { get; set; }
    public virtual DbSet<ReliefItem> ReliefItems { get; set; }
    public virtual DbSet<RescuerApplication> RescuerApplications { get; set; }
    public virtual DbSet<RescuerApplicationDocument> RescuerApplicationDocuments { get; set; }
    public virtual DbSet<RescueTeam> RescueTeams { get; set; }
    public virtual DbSet<RescueTeamAiSuggestion> RescueTeamAiSuggestions { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<RolePermission> RolePermissions { get; set; }
    public virtual DbSet<SosAiAnalysis> SosAiAnalyses { get; set; }
    public virtual DbSet<SosCluster> SosClusters { get; set; }
    public virtual DbSet<SosRequest> SosRequests { get; set; }
    public virtual DbSet<SosRequestUpdate> SosRequestUpdates { get; set; }
    public virtual DbSet<SosRuleEvaluation> SosRuleEvaluations { get; set; }
    public virtual DbSet<TeamIncident> TeamIncidents { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserAbility> UserAbilities { get; set; }
    public virtual DbSet<UserNotification> UserNotifications { get; set; }
    public virtual DbSet<UserPermission> UserPermissions { get; set; }
    public virtual DbSet<VatInvoice> VatInvoices { get; set; }
    public virtual DbSet<Vehicle> Vehicles { get; set; }
    public virtual DbSet<VehicleActivityLog> VehicleActivityLogs { get; set; }
    public virtual DbSet<VehicleCategory> VehicleCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Ability>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("abilities_pkey");
        });

        modelBuilder.Entity<ActivityAiSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("activity_ai_suggestions_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ClusterAiAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cluster_ai_analysis_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversations_pkey");
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversation_participants_pkey");
        });

        modelBuilder.Entity<Depot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depots_pkey");
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_logs_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("missions_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<MissionActivity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_activities_pkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organizations_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<OrganizationReliefItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_relief_items_pkey");
        });

        modelBuilder.Entity<Prompt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prompts_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ReliefItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("relief_items_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<RescueTeamAiSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rescue_team_ai_suggestions_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<SosAiAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sos_ai_analysis_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<SosCluster>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sos_clusters_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<SosRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sos_requests_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<UserAbility>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.AbilityId }).HasName("user_abilities_pkey");
        });
        
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ClaimId }).HasName("user_permissions_pkey");
        });
        
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.ClaimId }).HasName("role_permissions_pkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
