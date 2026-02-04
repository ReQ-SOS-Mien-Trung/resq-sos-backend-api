using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class OperationsSeeder
{
    public static void SeedOperations(this ModelBuilder modelBuilder)
    {
        SeedMissions(modelBuilder);
        SeedMissionActivities(modelBuilder);
        SeedMissionItems(modelBuilder);
        SeedConversations(modelBuilder);
        SeedConversationParticipants(modelBuilder);
        SeedMessages(modelBuilder);
    }

    private static void SeedMissions(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Mission>().HasData(
            // Mission 1: Rescue in Le Thuy
            new Mission
            {
                Id = 1,
                ClusterId = 1,
                MissionType = "Rescue",
                PriorityScore = 10.0,
                Status = "InProgress",
                StartTime = now,
                ExpectedEndTime = now.AddHours(6),
                CreatedAt = now,
                CreatedById = SeedConstants.CoordinatorUserId
            },
            // Mission 2: Relief Distribution in Huong Tra
            new Mission
            {
                Id = 2,
                ClusterId = 2,
                MissionType = "Relief",
                PriorityScore = 7.0,
                Status = "Planned",
                StartTime = now.AddHours(2),
                ExpectedEndTime = now.AddHours(8),
                CreatedAt = now,
                CreatedById = SeedConstants.AdminUserId
            }
        );
    }

    private static void SeedMissionActivities(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 15, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionActivity>().HasData(
            new MissionActivity
            {
                Id = 1,
                MissionId = 1,
                Step = 1,
                ActivityCode = "EVACUATE",
                ActivityType = "Evacuation",
                Description = "Tiếp cận khu vực ngập sâu Lệ Thủy, hỗ trợ y tế và di tản.",
                Target = "{\"location\": \"Xã An Thủy\", \"count\": 30}",
                TargetLocation = new Point(106.7865, 17.2140) { SRID = 4326 },
                Status = "InProgress",
                AssignedAt = now,
                LastDecisionBy = SeedConstants.CoordinatorUserId
            },
            new MissionActivity
            {
                Id = 2,
                MissionId = 2,
                Step = 1,
                ActivityCode = "DISTRIBUTE",
                ActivityType = "Distribution",
                Description = "Phân phát lương thực cứu trợ (Gạo, mỳ) tại Hương Toàn.",
                Target = "{\"items\": [\"rice\", \"food\"], \"count\": 200}",
                TargetLocation = new Point(107.4566, 16.3986) { SRID = 4326 },
                Status = "Planned",
                AssignedAt = now,
                LastDecisionBy = SeedConstants.AdminUserId
            }
        );
    }

    private static void SeedMissionItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MissionItem>().HasData(
            // Mission 1 (Rescue): Needs Medical/Rescue Kits. 
            // FIX: Use ReliefItem ID 2 (First Aid/Medical) which definitely exists.
            new MissionItem
            {
                Id = 1,
                ReliefItemId = 2, 
                MissionId = 1, 
                RequiredQuantity = 20,
                AllocatedQuantity = 20,
                SourceDepotId = 2 // Le Thuy Depot
            },
            // Mission 2 (Relief): Needs Food.
            // FIX: Use ReliefItem ID 1 (Rice/Food) which definitely exists.
            new MissionItem
            {
                Id = 2,
                ReliefItemId = 1, 
                MissionId = 2, 
                RequiredQuantity = 100,
                AllocatedQuantity = 100,
                SourceDepotId = 1 // Hue Depot
            }
            // Removed MissionItem 3 & 4 to prevent FK errors with new/unstable ReliefItem IDs.
        );
    }

    private static void SeedConversations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>().HasData(
            new Conversation { Id = 1, MissionId = 1 },
            new Conversation { Id = 2, MissionId = 2 }
        );
    }

    private static void SeedConversationParticipants(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ConversationParticipant>().HasData(
            new ConversationParticipant { Id = 1, ConversationId = 1, UserId = SeedConstants.AdminUserId, RoleInConversation = "Monitor", JoinedAt = now },
            new ConversationParticipant { Id = 2, ConversationId = 1, UserId = SeedConstants.RescuerUserId, RoleInConversation = "Leader", JoinedAt = now },
            new ConversationParticipant { Id = 3, ConversationId = 2, UserId = SeedConstants.CoordinatorUserId, RoleInConversation = "Logistics", JoinedAt = now }
        );
    }

    private static void SeedMessages(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 5, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Message>().HasData(
            new Message
            {
                Id = 1,
                ConversationId = 1,
                SenderId = SeedConstants.RescuerUserId,
                Content = "Đội đã tiếp cận được đầu làng. Đang sử dụng vật tư y tế để sơ cứu người bị thương.",
                CreatedAt = now
            },
            new Message
            {
                Id = 2,
                ConversationId = 2,
                SenderId = SeedConstants.CoordinatorUserId,
                Content = "Đã xuất kho 100 bao gạo từ kho Huế, xe đang di chuyển.",
                CreatedAt = now
            }
        );
    }
}
