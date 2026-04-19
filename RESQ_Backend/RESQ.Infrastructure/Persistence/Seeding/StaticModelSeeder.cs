using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class StaticModelSeeder
{
    /// <summary>
    /// Seeds all static model data in correct FK dependency order.
    /// Order: System → Permission → Identity → Personnel → Emergency → Logistics → Operations → AiAnalysis
    /// </summary>
    public static void SeedStaticModelData(this ModelBuilder modelBuilder)
    {
        modelBuilder.SeedSystem();        // notifications, ai configs, prompts, service zone, cluster grouping config, priority rules
        modelBuilder.SeedPermission();    // permissions, role_permissions
        modelBuilder.SeedIdentity();      // roles, document_file_type_categories, document_file_types, users, rescuer_applications
        modelBuilder.SeedPersonnel();     // ability_categories, ability_subgroups, abilities, assembly_points, rescue_teams (needs users)
        modelBuilder.SeedEmergency();     // sos_clusters, sos_requests, rule_evaluations (needs users)
        modelBuilder.SeedLogistics();     // categories, depots, depot_managers, items, inventories (needs users)
        modelBuilder.SeedOperations();    // missions (needs clusters), activities, teams, team_members (needs users, clusters)
        modelBuilder.SeedAiAnalysis();    // cluster/activity/team ai analyses (needs clusters, missions, rescue_teams)
    }
}
