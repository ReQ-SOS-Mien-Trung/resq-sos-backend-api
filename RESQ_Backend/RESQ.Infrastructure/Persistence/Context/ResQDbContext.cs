using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
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
    public virtual DbSet<DepotClosure> DepotClosures { get; set; }
    public virtual DbSet<DepotClosureTransfer> DepotClosureTransfers { get; set; }
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
    public virtual DbSet<SupplyRequestPriorityConfig> SupplyRequestPriorityConfigs { get; set; }
    public virtual DbSet<Donation> Donations { get; set; }
    public virtual DbSet<FundCampaign> FundCampaigns { get; set; }
    public virtual DbSet<FundTransaction> FundTransactions { get; set; }
    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<Mission> Missions { get; set; }
    public virtual DbSet<MissionActivity> MissionActivities { get; set; }
    public virtual DbSet<MissionActivityReport> MissionActivityReports { get; set; }
    public virtual DbSet<MissionAiSuggestion> MissionAiSuggestions { get; set; }
    public virtual DbSet<MissionItem> MissionItems { get; set; }
    public virtual DbSet<MissionTeam> MissionTeams { get; set; }
    public virtual DbSet<MissionTeamReport> MissionTeamReports { get; set; }
    public virtual DbSet<MissionTeamMember> MissionTeamMembers { get; set; }
    public virtual DbSet<MissionTeamMemberEvaluation> MissionTeamMemberEvaluations { get; set; }
    public virtual DbSet<ReusableItem> ReusableItems { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<DepotRealtimeOutbox> DepotRealtimeOutboxEvents { get; set; }
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
    public virtual DbSet<RescuerProfile> RescuerProfiles { get; set; }
    public virtual DbSet<RescuerScore> RescuerScores { get; set; }
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
    public virtual DbSet<StockWarningBandConfig> StockWarningBandConfigs { get; set; }
    public virtual DbSet<SystemMigrationAudit> SystemMigrationAudits { get; set; }
    public virtual DbSet<UserRelativeProfile> UserRelativeProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasSequence<long>("depot_realtime_version_seq");
        
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

        modelBuilder.Entity<RescuerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("rescuer_profiles_pkey");
            entity.HasOne(e => e.User)
                .WithOne(u => u.RescuerProfile)
                .HasForeignKey<RescuerProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_rescuer_profiles_users_user_id");

            entity.HasOne(e => e.ApprovedByUser)
                .WithMany(u => u.ApprovedRescuerProfiles)
                .HasForeignKey(e => e.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_rescuer_profiles_users_approved_by");
        });

        modelBuilder.Entity<RescuerScore>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("rescuer_scores_pkey");

            entity.HasOne(e => e.RescuerProfile)
                .WithOne(p => p.RescuerScore)
                .HasForeignKey<RescuerScore>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_rescuer_scores_rescuer_profiles_user_id");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_rescuer_scores_response_time_score_range", "\"response_time_score\" >= 0 AND \"response_time_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_rescue_effectiveness_score_range", "\"rescue_effectiveness_score\" >= 0 AND \"rescue_effectiveness_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_decision_handling_score_range", "\"decision_handling_score\" >= 0 AND \"decision_handling_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_safety_medical_skill_score_range", "\"safety_medical_skill_score\" >= 0 AND \"safety_medical_skill_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_teamwork_communication_score_range", "\"teamwork_communication_score\" >= 0 AND \"teamwork_communication_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_overall_average_score_range", "\"overall_average_score\" >= 0 AND \"overall_average_score\" <= 10");
                t.HasCheckConstraint("CK_rescuer_scores_evaluation_count_non_negative", "\"evaluation_count\" >= 0");
            });
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

        modelBuilder.Entity<SupplyInventory>(entity =>
        {
            entity.UseXminAsConcurrencyToken();
            entity.Ignore(e => e.TotalReservedQuantity);
        });

        modelBuilder.Entity<DepotClosure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_closures_pkey");

            // Chỉ 1 closure InProgress/Processing per depot — database-level guard
            entity.HasIndex(e => e.DepotId)
                  .HasDatabaseName("uix_depot_closures_active")
                  .HasFilter("status IN ('InProgress', 'Processing')")
                  .IsUnique();

            // Index cho timeout daemon
            entity.HasIndex(e => e.ClosingTimeoutAt)
                  .HasDatabaseName("ix_depot_closures_timeout_sweep")
                  .HasFilter("status = 'InProgress'");

            entity.HasOne(e => e.Depot)
                .WithMany()
                .HasForeignKey(e => e.DepotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetDepot)
                .WithMany()
                .HasForeignKey(e => e.TargetDepotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DepotClosureTransfer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_closure_transfers_pkey");

            // Index để tìm transfer theo closure_id nhanh
            entity.HasIndex(e => e.ClosureId)
                  .HasDatabaseName("ix_depot_closure_transfers_closure_id");

            // Chỉ 1 transfer active per closure
            entity.HasIndex(e => e.ClosureId)
                  .HasDatabaseName("uix_depot_closure_transfers_active")
                  .HasFilter("status NOT IN ('Completed', 'Cancelled')")
                  .IsUnique();

            entity.HasOne(e => e.Closure)
                .WithMany()
                .HasForeignKey(e => e.ClosureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplyRequestPriorityConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("supply_request_priority_configs_pkey");
            entity.HasCheckConstraint(
                "ck_supply_request_priority_configs_order",
                "urgent_minutes > 0 AND urgent_minutes < high_minutes AND high_minutes < medium_minutes");
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
                "(danger_ratio IS NULL AND warning_ratio IS NULL) OR " +
                "(danger_ratio > 0 AND danger_ratio < warning_ratio AND warning_ratio <= 1)");

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

        modelBuilder.Entity<StockWarningBandConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("stock_warning_band_config_pkey");
            entity.HasData(new StockWarningBandConfig
            {
                Id = 1,
                BandsJson = "[{\"name\":\"CRITICAL\",\"from\":0.0,\"to\":0.4},{\"name\":\"MEDIUM\",\"from\":0.4,\"to\":0.7},{\"name\":\"LOW\",\"from\":0.7,\"to\":1.0},{\"name\":\"OK\",\"from\":1.0,\"to\":null}]",
                UpdatedBy = null,
                UpdatedAt = new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<DepotRealtimeOutbox>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("depot_realtime_outbox_pkey");
            entity.Property(e => e.Version)
                .HasDefaultValueSql("nextval('depot_realtime_version_seq')");
            entity.Property(e => e.Status)
                .HasDefaultValue("Pending");
            entity.Property(e => e.AttemptCount)
                .HasDefaultValue(0);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.DepotId, e.Version })
                .HasDatabaseName("ix_depot_realtime_outbox_depot_version");
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt })
                .HasDatabaseName("ix_depot_realtime_outbox_status_next_attempt");
        });

        modelBuilder.Entity<MissionTeamReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_team_reports_pkey");
            entity.HasIndex(e => e.MissionTeamId)
                .HasDatabaseName("ux_mission_team_reports_mission_team_id")
                .IsUnique();

            entity.HasOne(e => e.MissionTeam)
                .WithOne(mt => mt.MissionTeamReport)
                .HasForeignKey<MissionTeamReport>(e => e.MissionTeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SubmittedByUser)
                .WithMany()
                .HasForeignKey(e => e.SubmittedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MissionTeamMemberEvaluation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_team_member_evaluations_pkey");
            entity.HasIndex(e => new { e.MissionTeamReportId, e.RescuerId })
                .HasDatabaseName("ux_mission_team_member_evaluations_report_rescuer")
                .IsUnique();

            entity.HasOne(e => e.MissionTeamReport)
                .WithMany(r => r.MissionTeamMemberEvaluations)
                .HasForeignKey(e => e.MissionTeamReportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RescuerProfile)
                .WithMany(p => p.MissionTeamMemberEvaluations)
                .HasForeignKey(e => e.RescuerId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_mission_team_member_evaluations_rescuer_profiles_rescuer_id");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_mission_team_member_evaluations_response_time_score_range", "\"response_time_score\" >= 0 AND \"response_time_score\" <= 10");
                t.HasCheckConstraint("CK_mission_team_member_evaluations_rescue_effectiveness_score_range", "\"rescue_effectiveness_score\" >= 0 AND \"rescue_effectiveness_score\" <= 10");
                t.HasCheckConstraint("CK_mission_team_member_evaluations_decision_handling_score_range", "\"decision_handling_score\" >= 0 AND \"decision_handling_score\" <= 10");
                t.HasCheckConstraint("CK_mission_team_member_evaluations_safety_medical_skill_score_range", "\"safety_medical_skill_score\" >= 0 AND \"safety_medical_skill_score\" <= 10");
                t.HasCheckConstraint("CK_mission_team_member_evaluations_teamwork_communication_score_range", "\"teamwork_communication_score\" >= 0 AND \"teamwork_communication_score\" <= 10");
            });
        });

        modelBuilder.Entity<MissionActivityReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mission_activity_reports_pkey");
            entity.HasIndex(e => new { e.MissionTeamReportId, e.MissionActivityId })
                .HasDatabaseName("ux_mission_activity_reports_team_report_activity")
                .IsUnique();

            entity.HasOne(e => e.MissionTeamReport)
                .WithMany(r => r.MissionActivityReports)
                .HasForeignKey(e => e.MissionTeamReportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MissionActivity)
                .WithMany(a => a.MissionActivityReports)
                .HasForeignKey(e => e.MissionActivityId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<UserRelativeProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_relative_profiles_pkey");

            entity.Property(e => e.MedicalProfileJson)
                .HasDefaultValueSql("'{}'::jsonb");

            entity.HasOne(e => e.User)
                .WithMany(u => u.RelativeProfiles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_user_relative_profiles_users_user_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("ix_user_relative_profiles_user_id");

            entity.HasIndex(e => new { e.UserId, e.ProfileUpdatedAt })
                .HasDatabaseName("ix_user_relative_profiles_user_id_profile_updated_at");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_user_relative_profiles_person_type",
                    "person_type IN ('ADULT','CHILD','ELDERLY')");
                t.HasCheckConstraint(
                    "ck_user_relative_profiles_relation_group",
                    "relation_group IN ('gia_dinh','nha_noi','nha_ngoai','hang_xom','ban_be','khac')");
                t.HasCheckConstraint(
                    "ck_user_relative_profiles_gender",
                    "gender IS NULL OR gender IN ('MALE','FEMALE')");
            });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    public override int SaveChanges()
    {
        CaptureDepotRealtimeOutboxEntries();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CaptureDepotRealtimeOutboxEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureDepotRealtimeOutboxEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        CaptureDepotRealtimeOutboxEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private bool _isCapturingOutbox;

    private void CaptureDepotRealtimeOutboxEntries()
    {
        if (_isCapturingOutbox)
            return;

        _isCapturingOutbox = true;
        try
        {
            var depotEntries = ChangeTracker.Entries<Depot>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            var depotManagerEntries = ChangeTracker.Entries<DepotManager>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            var supplyEntries = ChangeTracker.Entries<SupplyInventory>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            var reusableEntries = ChangeTracker.Entries<ReusableItem>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            if (depotEntries.Count == 0 && depotManagerEntries.Count == 0 && supplyEntries.Count == 0 && reusableEntries.Count == 0)
                return;

            var missionByDepot = BuildMissionContextMapFromInventoryLogs();
            var snapshots = new Dictionary<int, object>();
            var changedFields = new Dictionary<int, HashSet<string>>();
            var priority = new Dictionary<int, (string Operation, string PayloadKind, bool IsCritical)>();

            foreach (var entry in depotEntries)
            {
                var depotId = ResolveDepotId(entry);
                if (!depotId.HasValue) continue;

                (string Operation, string PayloadKind, bool IsCritical) op = entry.State switch
                {
                    EntityState.Added => (Operation: "Create", PayloadKind: "Full", IsCritical: true),
                    EntityState.Deleted => (Operation: "Delete", PayloadKind: "Full", IsCritical: true),
                    _ => (Operation: "Update", PayloadKind: "Full", IsCritical: true)
                };
                MergePriority(priority, depotId.Value, op.Operation, op.PayloadKind, op.IsCritical);

                if (entry.State == EntityState.Modified)
                {
                    var fields = entry.Properties
                        .Where(p => p.IsModified)
                        .Select(p => p.Metadata.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (fields.Count > 0)
                    {
                        if (!changedFields.TryGetValue(depotId.Value, out var set))
                        {
                            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            changedFields[depotId.Value] = set;
                        }

                        foreach (var field in fields)
                            set.Add(field);
                    }
                }
            }

            foreach (var entry in depotManagerEntries)
            {
                var depotId = ResolveDepotId(entry);
                if (!depotId.HasValue) continue;

                MergePriority(priority, depotId.Value, "Update", "Full", true);

                if (!changedFields.TryGetValue(depotId.Value, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    changedFields[depotId.Value] = set;
                }

                set.Add("Manager");
            }

            foreach (var entry in supplyEntries)
            {
                var depotId = ResolveDepotId(entry);
                if (!depotId.HasValue) continue;

                (string Operation, string PayloadKind, bool IsCritical) op = entry.State switch
                {
                    EntityState.Added => (Operation: "Import", PayloadKind: "Full", IsCritical: true),
                    EntityState.Deleted => (Operation: "Delete", PayloadKind: "Full", IsCritical: true),
                    _ => (Operation: "Update", PayloadKind: "Delta", IsCritical: false)
                };

                MergePriority(priority, depotId.Value, op.Operation, op.PayloadKind, op.IsCritical);

                if (entry.State == EntityState.Modified)
                {
                    var delta = entry.Properties
                        .Where(p => p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

                    if (delta.Count > 0)
                    {
                        snapshots[depotId.Value] = delta;

                        if (!changedFields.TryGetValue(depotId.Value, out var set))
                        {
                            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            changedFields[depotId.Value] = set;
                        }

                        foreach (var field in delta.Keys)
                            set.Add(field);
                    }
                }
            }

            foreach (var entry in reusableEntries)
            {
                var depotIds = ResolveAffectedDepotIds(entry);
                foreach (var depotId in depotIds)
                {
                    (string Operation, string PayloadKind, bool IsCritical) op = entry.State switch
                    {
                        EntityState.Added   => (Operation: "Import",  PayloadKind: "Full", IsCritical: true),
                        EntityState.Deleted => (Operation: "Delete",  PayloadKind: "Full", IsCritical: true),
                        _                  => (Operation: "Update",   PayloadKind: "Full", IsCritical: false)
                    };
                    MergePriority(priority, depotId, op.Operation, op.PayloadKind, op.IsCritical);

                    if (!changedFields.TryGetValue(depotId, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        changedFields[depotId] = set;
                    }
                    set.Add("ReusableItems");
                }
            }

            var now = DateTime.UtcNow;
            foreach (var (depotId, decision) in priority)
            {
                var outbox = new DepotRealtimeOutbox
                {
                    Id = Guid.NewGuid(),
                    DepotId = depotId,
                    MissionId = missionByDepot.TryGetValue(depotId, out var missionId) ? missionId : null,
                    EventType = "DepotUpdated",
                    Operation = decision.Operation,
                    PayloadKind = decision.PayloadKind,
                    IsCritical = decision.IsCritical,
                    ChangedFields = changedFields.TryGetValue(depotId, out var set) && set.Count > 0
                        ? string.Join(',', set.OrderBy(x => x))
                        : null,
                    SnapshotPayload = decision.PayloadKind == "Delta"
                        && snapshots.TryGetValue(depotId, out var snapshot)
                            ? JsonSerializer.Serialize(snapshot)
                            : null,
                    Status = "Pending",
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    OccurredAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                DepotRealtimeOutboxEvents.Add(outbox);
            }
        }
        finally
        {
            _isCapturingOutbox = false;
        }
    }

    private static int? ResolveDepotId(EntityEntry<Depot> entry)
    {
        return entry.State switch
        {
            EntityState.Added => entry.Entity.Id > 0 ? entry.Entity.Id : null,
            EntityState.Modified => entry.Entity.Id,
            EntityState.Deleted => (int?)entry.OriginalValues[nameof(Depot.Id)],
            _ => null
        };
    }

    private static int? ResolveDepotId(EntityEntry<DepotManager> entry)
    {
        return entry.State switch
        {
            EntityState.Deleted => entry.OriginalValues[nameof(DepotManager.DepotId)] is int depotId ? depotId : null,
            _ => entry.Entity.DepotId
        };
    }

    private static int? ResolveDepotId(EntityEntry<SupplyInventory> entry)
    {
        return entry.State switch
        {
            EntityState.Deleted => entry.OriginalValues[nameof(SupplyInventory.DepotId)] is int depotId ? depotId : null,
            _ => entry.Entity.DepotId
        };
    }

    /// <summary>
    /// Returns all depot IDs affected by a ReusableItem change.
    /// — For Reserve/InUse/status-only changes: just the current DepotId (unchanged).
    /// — For TransferIn (null → depotId): the new depot.
    /// — For TransferOut (depotId → null): the original depot (captured before null-out, see note below).
    /// NOTE: When UpdateAsync is called AFTER setting DepotId=null (TransferOut path), both
    /// Entity.DepotId and OriginalValues[DepotId] are null because EF attached the detached
    /// entity with null; in that case no depot can be resolved here — the Ship step is handled
    /// by a separate explicit outbox entry in TransferOutAsync.
    /// </summary>
    private static IEnumerable<int> ResolveAffectedDepotIds(EntityEntry<ReusableItem> entry)
    {
        var result = new HashSet<int>();

        // Current depot (covers Reserve, InUse, Maintenance, TransferIn)
        if (entry.Entity.DepotId.HasValue)
            result.Add(entry.Entity.DepotId.Value);

        // Original depot for Modified/Deleted in case DepotId changed
        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            if (entry.OriginalValues[nameof(ReusableItem.DepotId)] is int originalDepotId)
                result.Add(originalDepotId);
        }

        return result;
    }

    private Dictionary<int, int?> BuildMissionContextMapFromInventoryLogs()
    {
        var result = new Dictionary<int, int?>();
        var inventoryById = ChangeTracker.Entries<SupplyInventory>()
            .ToDictionary(x => x.Entity.Id, x => x.Entity.DepotId);

        var logEntries = ChangeTracker.Entries<InventoryLog>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        foreach (var log in logEntries)
        {
            var depotId = log.SupplyInventory?.DepotId;
            if (!depotId.HasValue && log.DepotSupplyInventoryId.HasValue && inventoryById.TryGetValue(log.DepotSupplyInventoryId.Value, out var mapped))
            {
                depotId = mapped;
            }

            if (depotId.HasValue)
            {
                result[depotId.Value] = log.MissionId;
            }
        }

        return result;
    }

    private static void MergePriority(
        IDictionary<int, (string Operation, string PayloadKind, bool IsCritical)> map,
        int depotId,
        string operation,
        string payloadKind,
        bool isCritical)
    {
        if (!map.TryGetValue(depotId, out var current))
        {
            map[depotId] = (operation, payloadKind, isCritical);
            return;
        }

        // Full/Critical luôn ưu tiên hơn Delta/non-critical.
        if (isCritical && !current.IsCritical)
        {
            map[depotId] = (operation, payloadKind, isCritical);
            return;
        }

        if (current.IsCritical)
        {
            // Giữ loại operation có mức ưu tiên cao hơn trong critical set.
            var nextRank = GetOperationRank(operation);
            var currentRank = GetOperationRank(current.Operation);
            if (nextRank > currentRank)
            {
                map[depotId] = (operation, current.PayloadKind, true);
            }
            return;
        }

        map[depotId] = (operation, payloadKind, isCritical);
    }

    private static int GetOperationRank(string operation)
    {
        return operation switch
        {
            "Delete" => 6,
            "Transfer" => 5,
            "Import" => 4,
            "Create" => 3,
            "Export" => 2,
            _ => 1
        };
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
