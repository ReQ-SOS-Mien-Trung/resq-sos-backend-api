using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class AiAnalysisSeeder
{
    public static void SeedAiAnalysis(this ModelBuilder modelBuilder)
    {
        SeedClusterAiAnalyses(modelBuilder);
        SeedActivityAiSuggestions(modelBuilder);
        SeedRescueTeamAiSuggestions(modelBuilder);
    }

    private static void SeedClusterAiAnalyses(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 15, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ClusterAiAnalysis>().HasData(
            new ClusterAiAnalysis
            {
                Id = 1,
                ClusterId = 1, // Le Thuy
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Severity",
                Metadata = "{\"event_assessment\": {\"severity\": \"critical\", \"risk_factors\": [\"rapid_water_rise\", \"night_time\"]}, \"suggested_plan\": {\"actions\": [\"deploy_boats\", \"prioritize_vulnerable\"]}}",
                ConfidenceScore = 0.92,
                SuggestedSeverityLevel = "Critical",
                CreatedAt = now
            },
            new ClusterAiAnalysis
            {
                Id = 2,
                ClusterId = 2, // Huong Tra
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Resource",
                Metadata = "{\"event_assessment\": {\"severity\": \"high\", \"risk_factors\": [\"road_blocked\"]}, \"suggested_plan\": {\"actions\": [\"air_drop_supplies\", \"use_amphibious_vehicles\"]}}",
                ConfidenceScore = 0.85,
                SuggestedSeverityLevel = "High",
                CreatedAt = now
            }
        );
    }

    private static void SeedActivityAiSuggestions(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 20, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ActivityAiSuggestion>().HasData(
            new ActivityAiSuggestion
            {
                Id = 1,
                ClusterId = 1,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                ActivityType = "Evacuation",
                SuggestionPhase = "Execution",
                SuggestedActivities = "{\"steps\": [\"scout_safe_path\", \"transport_elderly_first\", \"mark_cleared_houses\"]}",
                ConfidenceScore = 0.9,
                CreatedAt = now
            },
            new ActivityAiSuggestion
            {
                Id = 2,
                ClusterId = 2,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                ActivityType = "Distribution",
                SuggestionPhase = "Planning",
                SuggestedActivities = "{\"steps\": [\"verify_road_access\", \"prepare_dry_food\", \"coordinate_with_local_militia\"]}",
                ConfidenceScore = 0.88,
                CreatedAt = now
            }
        );
    }

    private static void SeedRescueTeamAiSuggestions(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 25, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeamAiSuggestion>().HasData(
            new RescueTeamAiSuggestion
            {
                Id = 1,
                ClusterId = 1,
                AdoptedRescueTeamId = 1, // Doi Cuu Ho Song Huong
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Assignment",
                SuggestionScope = "{\"reasons\": [\"closest_proximity\", \"flood_experience\"]}",
                ConfidenceScore = 0.95,
                CreatedAt = now
            },
            new RescueTeamAiSuggestion
            {
                Id = 2,
                ClusterId = 2,
                AdoptedRescueTeamId = 1, // Hue Team for Hue Cluster
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Assignment",
                SuggestionScope = "{\"reasons\": [\"local_knowledge\", \"available_equipment\"]}",
                ConfidenceScore = 0.89,
                CreatedAt = now
            }
        );
    }
}
