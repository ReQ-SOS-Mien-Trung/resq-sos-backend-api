using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Entities;
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

    public virtual DbSet<ActivityHandoverLog> ActivityHandoverLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ClusterAiAnalysis> ClusterAiAnalyses { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public virtual DbSet<Depot> Depots { get; set; }

    public virtual DbSet<DepotInventory> DepotInventories { get; set; }

    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Mission> Missions { get; set; }

    public virtual DbSet<MissionActivity> MissionActivities { get; set; }

    public virtual DbSet<MissionItem> MissionItems { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationReliefItem> OrganizationReliefItems { get; set; }

    public virtual DbSet<Prompt> Prompts { get; set; }

    public virtual DbSet<ReliefItem> ReliefItems { get; set; }

    public virtual DbSet<RescueUnit> RescueUnits { get; set; }

    public virtual DbSet<RescueUnitAiSuggestion> RescueUnitAiSuggestions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SosAiAnalysis> SosAiAnalyses { get; set; }

    public virtual DbSet<SosCluster> SosClusters { get; set; }

    public virtual DbSet<SosRequest> SosRequests { get; set; }

    public virtual DbSet<UnitMember> UnitMembers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAbility> UserAbilities { get; set; }

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

            entity.HasOne(d => d.Cluster).WithMany(p => p.ActivityAiSuggestions).HasConstraintName("activity_ai_suggestions_cluster_id_fkey");
        });

        modelBuilder.Entity<ActivityHandoverLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("activity_handover_logs_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Activity).WithMany(p => p.ActivityHandoverLogs).HasConstraintName("activity_handover_logs_activity_id_fkey");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.ActivityHandoverLogs).HasConstraintName("activity_handover_logs_decided_by_fkey");

            entity.HasOne(d => d.FromUnit).WithMany(p => p.ActivityHandoverLogFromUnits).HasConstraintName("activity_handover_logs_from_unit_id_fkey");

            entity.HasOne(d => d.ToUnit).WithMany(p => p.ActivityHandoverLogToUnits).HasConstraintName("activity_handover_logs_to_unit_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ClusterAiAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cluster_ai_analysis_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Cluster).WithMany(p => p.ClusterAiAnalyses).HasConstraintName("cluster_ai_analysis_cluster_id_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversations_pkey");

            entity.HasOne(d => d.Mission).WithMany(p => p.Conversations).HasConstraintName("conversations_mission_id_fkey");
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId }).HasName("conversation_participants_pkey");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationParticipants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("conversation_participants_conversation_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationParticipants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("conversation_participants_user_id_fkey");
        });

        modelBuilder.Entity<Depot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depots_pkey");

            entity.HasOne(d => d.DepotManager).WithMany(p => p.Depots).HasConstraintName("depots_depot_manager_id_fkey");
        });

        modelBuilder.Entity<DepotInventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_inventory_pkey");

            entity.HasOne(d => d.Depot).WithMany(p => p.DepotInventories).HasConstraintName("depot_inventory_depot_id_fkey");

            entity.HasOne(d => d.ReliefItem).WithMany(p => p.DepotInventories).HasConstraintName("depot_inventory_relief_item_id_fkey");
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_logs_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.DepotInventory).WithMany(p => p.InventoryLogs).HasConstraintName("inventory_logs_depot_inventory_id_fkey");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.InventoryLogs).HasConstraintName("inventory_logs_performed_by_fkey");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");

            entity.Property(e => e.SentAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages).HasConstraintName("messages_conversation_id_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages).HasConstraintName("messages_sender_id_fkey");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("missions_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Cluster).WithMany(p => p.Missions).HasConstraintName("missions_cluster_id_fkey");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.Missions).HasConstraintName("missions_coordinator_id_fkey");

            entity.HasOne(d => d.PrimaryUnit).WithMany(p => p.Missions).HasConstraintName("missions_primary_unit_id_fkey");
        });

        modelBuilder.Entity<MissionActivity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_activities_pkey");

            entity.HasOne(d => d.AssignedUnit).WithMany(p => p.MissionActivities).HasConstraintName("mission_activities_assigned_unit_id_fkey");

            entity.HasOne(d => d.LastDecisionByNavigation).WithMany(p => p.MissionActivities).HasConstraintName("mission_activities_last_decision_by_fkey");

            entity.HasOne(d => d.Mission).WithMany(p => p.MissionActivities).HasConstraintName("mission_activities_mission_id_fkey");
        });

        modelBuilder.Entity<MissionItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_items_pkey");

            entity.HasOne(d => d.Mission).WithMany(p => p.MissionItems).HasConstraintName("mission_items_mission_id_fkey");

            entity.HasOne(d => d.ReliefItem).WithMany(p => p.MissionItems).HasConstraintName("mission_items_relief_item_id_fkey");

            entity.HasOne(d => d.SourceDepot).WithMany(p => p.MissionItems).HasConstraintName("mission_items_source_depot_id_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications).HasConstraintName("notifications_user_id_fkey");
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

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationReliefItems).HasConstraintName("organization_relief_items_organization_id_fkey");

            entity.HasOne(d => d.ReliefItem).WithMany(p => p.OrganizationReliefItems).HasConstraintName("organization_relief_items_relief_item_id_fkey");
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

            entity.HasOne(d => d.Category).WithMany(p => p.ReliefItems).HasConstraintName("relief_items_category_id_fkey");
        });

        modelBuilder.Entity<RescueUnit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rescue_units_pkey");
        });

        modelBuilder.Entity<RescueUnitAiSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rescue_unit_ai_suggestions_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Cluster).WithMany(p => p.RescueUnitAiSuggestions).HasConstraintName("rescue_unit_ai_suggestions_cluster_id_fkey");

            entity.HasOne(d => d.SuggestedRescueUnit).WithMany(p => p.RescueUnitAiSuggestions).HasConstraintName("rescue_unit_ai_suggestions_suggested_rescue_unit_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");
        });

        modelBuilder.Entity<SosAiAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sos_ai_analysis_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.SosRequest).WithMany(p => p.SosAiAnalyses).HasConstraintName("sos_ai_analysis_sos_request_id_fkey");
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
            entity.Property(e => e.IsAnalyzed).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.SosRequests).HasConstraintName("sos_requests_user_id_fkey");
        });

        modelBuilder.Entity<UnitMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("unit_members_pkey");

            entity.HasOne(d => d.RescueUnit).WithMany(p => p.UnitMembers).HasConstraintName("unit_members_rescue_unit_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UnitMembers).HasConstraintName("unit_members_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Role).WithMany(p => p.Users).HasConstraintName("users_role_id_fkey");
        });

        modelBuilder.Entity<UserAbility>(entity =>
        {
            entity.HasKey(e => e.UserAbilityId).HasName("user_abilities_pkey");

            entity.HasOne(d => d.Ability).WithMany(p => p.UserAbilities).HasConstraintName("user_abilities_ability_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserAbilities).HasConstraintName("user_abilities_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
