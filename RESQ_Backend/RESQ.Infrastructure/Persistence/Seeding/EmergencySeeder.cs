using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
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
        var now = new DateTime(2024, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosCluster>().HasData(
            // Cluster 1: Le Thuy, Quang Binh - Deep Flooding
            new SosCluster
            {
                Id = 1,
                CenterLocation = new Point(106.7865, 17.2140) { SRID = 4326 },
                RadiusKm = 5.0,
                SeverityLevel = "Critical",
                WaterLevel = "Ngập sâu 2.5m",
                VictimEstimated = 150,
                ChildrenCount = 40,
                ElderlyCount = 50,
                MedicalUrgencyScore = 0.9,
                CreatedAt = now,
                LastUpdatedAt = now
            },
            // Cluster 2: Huong Tra, Hue - Isolation/Flash Flood
            new SosCluster
            {
                Id = 2,
                CenterLocation = new Point(107.4566, 16.3986) { SRID = 4326 },
                RadiusKm = 3.0,
                SeverityLevel = "High",
                WaterLevel = "Ngập 1.0m, chảy xiết",
                VictimEstimated = 60,
                ChildrenCount = 15,
                ElderlyCount = 20,
                MedicalUrgencyScore = 0.6,
                CreatedAt = now,
                LastUpdatedAt = now
            }
        );
    }

    private static void SeedSosRequests(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosRequest>().HasData(
            new SosRequest
            {
                Id = 1,
                ClusterId = 1,
                UserId = SeedConstants.VictimUserId, // A victim in Le Thuy
                Location = new Point(106.7860, 17.2145) { SRID = 4326 },
                RawMessage = "Cứu với, nước lên tới nóc nhà rồi. Có cụ già 80 tuổi bị liệt không di chuyển được.",
                PriorityLevel = "Critical",
                WaitTimeMinutes = 45,
                Status = "Pending",
                CreatedAt = now
            },
            new SosRequest
            {
                Id = 2,
                ClusterId = 2,
                UserId = SeedConstants.CoordinatorUserId, // Reporting on behalf of someone
                Location = new Point(107.4560, 16.3980) { SRID = 4326 },
                RawMessage = "Khu vực thôn X bị cô lập hoàn toàn, hết lương thực và nước uống 2 ngày nay.",
                PriorityLevel = "High",
                WaitTimeMinutes = 120,
                Status = "InProgress",
                CreatedAt = now
            }
        );
    }

    private static void SeedSosAiAnalyses(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 31, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosAiAnalysis>().HasData(
            new SosAiAnalysis
            {
                Id = 1,
                SosRequestId = 1,
                Metadata = "{\"urgency\": \"critical\", \"risk_factors\": [\"deep_water\", \"elderly_paralyzed\"], \"needs\": [\"boat_rescue\", \"medical\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "Critical",
                ConfidenceScore = 0.95,
                Explanation = "Phát hiện từ khóa 'nước tới nóc nhà', 'cụ già liệt'. Đánh giá nguy hiểm tính mạng cao.",
                CreatedAt = now
            },
            new SosAiAnalysis
            {
                Id = 2,
                SosRequestId = 2,
                Metadata = "{\"urgency\": \"high\", \"risk_factors\": [\"isolation\", \"food_shortage\"], \"needs\": [\"food\", \"water\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "High",
                ConfidenceScore = 0.88,
                Explanation = "Phát hiện từ khóa 'cô lập', 'hết lương thực'. Cần cứu trợ nhu yếu phẩm.",
                CreatedAt = now
            }
        );
    }
}
