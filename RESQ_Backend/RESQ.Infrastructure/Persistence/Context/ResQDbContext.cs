using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext : DbContext
{
    public ResQDbContext(DbContextOptions<ResQDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ability> Abilities { get; set; }
    public virtual DbSet<AbilityCategory> AbilityCategories { get; set; }
    public virtual DbSet<AbilitySubgroup> AbilitySubgroups { get; set; }
    public virtual DbSet<ActivityAiSuggestion> ActivityAiSuggestions { get; set; }
    public virtual DbSet<AssemblyPoint> AssemblyPoints { get; set; }
    public virtual DbSet<AssemblyEvent> AssemblyEvents { get; set; }
    public virtual DbSet<AssemblyParticipant> AssemblyParticipants { get; set; }
    public virtual DbSet<ClusterAiAnalysis> ClusterAiAnalyses { get; set; }
    public virtual DbSet<Conversation> Conversations { get; set; }
    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public virtual DbSet<Depot> Depots { get; set; }
    public virtual DbSet<CampaignDisbursement> CampaignDisbursements { get; set; }
    public virtual DbSet<DisbursementItem> DisbursementItems { get; set; }
    public virtual DbSet<FundingRequest> FundingRequests { get; set; }
    public virtual DbSet<FundingRequestItem> FundingRequestItems { get; set; }
    public virtual DbSet<DocumentFileType> DocumentFileTypes { get; set; }
    public virtual DbSet<DocumentFileTypeCategory> DocumentFileTypeCategories { get; set; }
    public virtual DbSet<DepotManager> DepotManagers { get; set; }
    public virtual DbSet<SupplyInventory> SupplyInventories { get; set; }
    public virtual DbSet<SupplyInventoryLot> SupplyInventoryLots { get; set; }
    public virtual DbSet<DepotSupplyRequest> DepotSupplyRequests { get; set; }
    public virtual DbSet<DepotSupplyRequestItem> DepotSupplyRequestItems { get; set; }
    public virtual DbSet<Donation> Donations { get; set; }
    public virtual DbSet<FundCampaign> FundCampaigns { get; set; }
    public virtual DbSet<FundTransaction> FundTransactions { get; set; }
    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<Mission> Missions { get; set; }
    public virtual DbSet<MissionActivity> MissionActivities { get; set; }
    public virtual DbSet<MissionAiSuggestion> MissionAiSuggestions { get; set; }
    public virtual DbSet<MissionItem> MissionItems { get; set; }
    public virtual DbSet<MissionTeam> MissionTeams { get; set; }
    public virtual DbSet<MissionTeamMember> MissionTeamMembers { get; set; }
    public virtual DbSet<ReusableItem> ReusableItems { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Organization> Organizations { get; set; }
    public virtual DbSet<OrganizationReliefItem> OrganizationReliefItems { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Prompt> Prompts { get; set; }
    public virtual DbSet<ServiceZone> ServiceZones { get; set; }
    public virtual DbSet<SosPriorityRuleConfig> SosPriorityRuleConfigs { get; set; }
    public virtual DbSet<ItemModel> ItemModels { get; set; }
    public virtual DbSet<TargetGroup> TargetGroups { get; set; }
    public virtual DbSet<RescuerApplication> RescuerApplications { get; set; }
    public virtual DbSet<RescuerApplicationDocument> RescuerApplicationDocuments { get; set; }
    public virtual DbSet<RescueTeam> RescueTeams { get; set; }
    public virtual DbSet<RescueTeamMember> RescueTeamMembers { get; set; } // Added
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
    public virtual DbSet<VatInvoiceItem> VatInvoiceItems { get; set; }
    public virtual DbSet<DepotFund> DepotFunds { get; set; }
    public virtual DbSet<DepotFundTransaction> DepotFundTransactions { get; set; }
    public virtual DbSet<InventoryStockThresholdConfig> InventoryStockThresholdConfigs { get; set; }
    public virtual DbSet<InventoryStockThresholdConfigHistory> InventoryStockThresholdConfigHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        
        modelBuilder.Entity<Ability>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("abilities_pkey");
            entity.HasOne(e => e.AbilitySubgroup)
                .WithMany(s => s.Abilities)
                .HasForeignKey(e => e.AbilitySubgroupId)
                .HasConstraintName("FK_abilities_ability_subgroups_ability_subgroup_id");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_methods_pkey");
            entity.HasIndex(e => e.Code).IsUnique();
        });
        
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ClaimId }).HasName("user_permissions_pkey");
        });
        
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.ClaimId }).HasName("role_permissions_pkey");
        });

        modelBuilder.Entity<DepotFund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_funds_pkey");
            entity.HasIndex(e => e.DepotId).IsUnique();
        });

        modelBuilder.Entity<DepotFundTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_fund_transactions_pkey");
        });

        modelBuilder.Entity<SupplyInventoryLot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("supply_inventory_lots_pkey");
            entity.HasIndex(e => new { e.SupplyInventoryId, e.RemainingQuantity, e.ExpiredDate })
                  .HasDatabaseName("ix_supply_inventory_lots_fefo");
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<InventoryStockThresholdConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_stock_threshold_configs_pkey");

            entity.HasOne(e => e.Depot)
                .WithMany()
                .HasForeignKey(e => e.DepotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ItemModel)
                .WithMany()
                .HasForeignKey(e => e.ItemModelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasCheckConstraint(
                "ck_inventory_stock_threshold_configs_ratio",
                "danger_ratio > 0 AND danger_ratio < warning_ratio AND warning_ratio <= 1");

            entity.HasCheckConstraint(
                "ck_inventory_stock_threshold_configs_scope",
                "(scope_type = 'GLOBAL' AND depot_id IS NULL AND category_id IS NULL AND item_model_id IS NULL) OR " +
                "(scope_type = 'DEPOT' AND depot_id IS NOT NULL AND category_id IS NULL AND item_model_id IS NULL) OR " +
                "(scope_type = 'DEPOT_CATEGORY' AND depot_id IS NOT NULL AND category_id IS NOT NULL AND item_model_id IS NULL) OR " +
                "(scope_type = 'DEPOT_ITEM' AND depot_id IS NOT NULL AND category_id IS NULL AND item_model_id IS NOT NULL)");

            entity.HasIndex(e => e.ScopeType)
                .HasDatabaseName("ix_stock_threshold_global_active")
                .HasFilter("scope_type = 'GLOBAL' AND is_active = true")
                .IsUnique();

            entity.HasIndex(e => new { e.ScopeType, e.DepotId })
                .HasDatabaseName("ix_stock_threshold_depot_active")
                .HasFilter("scope_type = 'DEPOT' AND is_active = true")
                .IsUnique();

            entity.HasIndex(e => new { e.ScopeType, e.DepotId, e.CategoryId })
                .HasDatabaseName("ix_stock_threshold_depot_category_active")
                .HasFilter("scope_type = 'DEPOT_CATEGORY' AND is_active = true")
                .IsUnique();

            entity.HasIndex(e => new { e.ScopeType, e.DepotId, e.ItemModelId })
                .HasDatabaseName("ix_stock_threshold_depot_item_active")
                .HasFilter("scope_type = 'DEPOT_ITEM' AND is_active = true")
                .IsUnique();

            entity.HasData(new InventoryStockThresholdConfig
            {
                Id = 1,
                ScopeType = "GLOBAL",
                DangerRatio = 0.2000m,
                WarningRatio = 0.4000m,
                IsActive = true,
                UpdatedBy = null,
                UpdatedAt = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
                RowVersion = 1
            });
        });

        modelBuilder.Entity<InventoryStockThresholdConfigHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_stock_threshold_config_history_pkey");
            entity.HasOne(e => e.Config)
                .WithMany()
                .HasForeignKey(e => e.ConfigId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TargetGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("target_groups_pkey");
            entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("target_groups_name_key");
            entity.HasData(
                new TargetGroup { Id = 1, Name = "Children" },
                new TargetGroup { Id = 2, Name = "Elderly" },
                new TargetGroup { Id = 3, Name = "Pregnant" },
                new TargetGroup { Id = 4, Name = "Adult" },
                new TargetGroup { Id = 5, Name = "Rescuer" }
            );
        });

        modelBuilder.Entity<ItemModel>()
            .HasMany(im => im.TargetGroups)
            .WithMany(tg => tg.ItemModels)
            .UsingEntity(
                "item_model_target_groups",
                l => l.HasOne(typeof(TargetGroup)).WithMany().HasForeignKey("target_group_id").HasConstraintName("FK_item_model_target_groups_target_group_id"),
                r => r.HasOne(typeof(ItemModel)).WithMany().HasForeignKey("item_model_id").HasConstraintName("FK_item_model_target_groups_item_model_id"),
                j => j.HasKey("item_model_id", "target_group_id").HasName("PK_item_model_target_groups")
            );

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
