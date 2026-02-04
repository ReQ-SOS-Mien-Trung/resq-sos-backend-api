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
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ClusterAiAnalysis>().HasData(
            new ClusterAiAnalysis
            {
                Id = 1,
                ClusterId = 1,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Severity",
                Metadata = "{\"event_assessment\": {\"severity\": \"high\", \"risk_factors\": [\"flooding\", \"elderly\"]}, \"suggested_plan\": {\"actions\": [\"evacuate\", \"medical_support\"]}}",
                ConfidenceScore = 0.85,
                CreatedAt = now
            },
            new ClusterAiAnalysis
            {
                Id = 2,
                ClusterId = 2,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Resource",
                Metadata = "{\"event_assessment\": {\"severity\": \"medium\", \"risk_factors\": [\"limited_supplies\"]}, \"suggested_plan\": {\"actions\": [\"distribute_food\", \"provide_shelter\"]}}",
                ConfidenceScore = 0.78,
                CreatedAt = now
            }
        );
    }

    private static void SeedActivityAiSuggestions(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ActivityAiSuggestion>().HasData(
            new ActivityAiSuggestion
            {
                Id = 1,
                ClusterId = 1,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                ActivityType = "Evacuation",
                SuggestionPhase = "Planning",
                SuggestedActivities = "{\"steps\": [\"identify_exits\", \"gather_people\", \"transport\"]}",
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
                SuggestionPhase = "Execution",
                SuggestedActivities = "{\"steps\": [\"inventory_check\", \"load_supplies\", \"distribute\"]}",
                ConfidenceScore = 0.82,
                CreatedAt = now
            }
        );
    }

    private static void SeedRescueTeamAiSuggestions(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeamAiSuggestion>().HasData(
            new RescueTeamAiSuggestion
            {
                Id = 1,
                ClusterId = 1,
                AdoptedRescueTeamId = 1,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Assignment",
                SuggestionScope = "{\"reasons\": [\"nearest\", \"available\", \"skilled\"]}",
                ConfidenceScore = 0.88,
                CreatedAt = now
            },
            new RescueTeamAiSuggestion
            {
                Id = 2,
                ClusterId = 2,
                AdoptedRescueTeamId = 2,
                ModelName = "GPT-4",
                ModelVersion = "v1.0",
                AnalysisType = "Assignment",
                SuggestionScope = "{\"reasons\": [\"capacity\", \"equipment\"]}",
                ConfidenceScore = 0.75,
                CreatedAt = now
            }
        );
    }
}