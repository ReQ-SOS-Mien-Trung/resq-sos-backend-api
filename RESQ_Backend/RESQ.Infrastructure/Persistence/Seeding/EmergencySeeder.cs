using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class EmergencySeeder
{
    public static void SeedEmergency(this ModelBuilder modelBuilder)
    {
        SeedSosClusters(modelBuilder);
        SeedSosRequests(modelBuilder);
        SeedSosAiAnalyses(modelBuilder);
    }

    private static void SeedSosClusters(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosCluster>().HasData(
            new SosCluster
            {
                Id = 1,
                RadiusKm = 2.5,
                SeverityLevel = "High",
                WaterLevel = "Ngập 1m",
                VictimEstimated = 50,
                ChildrenCount = 10,
                ElderlyCount = 15,
                MedicalUrgencyScore = 0.8,
                CreatedAt = now,
                LastUpdatedAt = now
            },
            new SosCluster
            {
                Id = 2,
                RadiusKm = 1.5,
                SeverityLevel = "Medium",
                WaterLevel = "Ngập 0.5m",
                VictimEstimated = 20,
                ChildrenCount = 5,
                ElderlyCount = 8,
                MedicalUrgencyScore = 0.5,
                CreatedAt = now,
                LastUpdatedAt = now
            }
        );
    }

    private static void SeedSosRequests(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosRequest>().HasData(
            new SosRequest
            {
                Id = 1,
                ClusterId = 1,
                UserId = SeedConstants.AdminUserId,
                RawMessage = "Cần cứu hộ khẩn cấp, có người già và trẻ em",
                PriorityLevel = "High",
                WaitTimeMinutes = 30,
                Status = "Pending",
                CreatedAt = now
            },
            new SosRequest
            {
                Id = 2,
                ClusterId = 2,
                UserId = SeedConstants.CoordinatorUserId,
                RawMessage = "Cần hỗ trợ thực phẩm và nước uống",
                PriorityLevel = "Medium",
                WaitTimeMinutes = 60,
                Status = "InProgress",
                CreatedAt = now
            }
        );
    }

    private static void SeedSosAiAnalyses(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosAiAnalysis>().HasData(
            new SosAiAnalysis
            {
                Id = 1,
                SosRequestId = 1,
                Metadata = "{\"urgency\": \"high\", \"needs\": [\"rescue\", \"medical\"]}",
                ModelVersion = "v1.0",
                CreatedAt = now
            },
            new SosAiAnalysis
            {
                Id = 2,
                SosRequestId = 2,
                Metadata = "{\"urgency\": \"medium\", \"needs\": [\"food\", \"water\"]}",
                ModelVersion = "v1.0",
                CreatedAt = now
            }
        );
    }
}