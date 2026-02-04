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
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Mission>().HasData(
            new Mission
            {
                Id = 1,
                ClusterId = 1,
                MissionType = "Rescue",
                PriorityScore = 10.0,
                Status = "InProgress",
                StartTime = now,
                ExpectedEndTime = now.AddHours(4),
                CreatedAt = now,
                CreatedById = SeedConstants.CoordinatorUserId
            },
            new Mission
            {
                Id = 2,
                ClusterId = 2,
                MissionType = "Relief",
                PriorityScore = 5.0,
                Status = "Planned",
                StartTime = now.AddHours(2),
                ExpectedEndTime = now.AddHours(6),
                CreatedAt = now,
                CreatedById = SeedConstants.AdminUserId
            }
        );
    }

    private static void SeedMissionActivities(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionActivity>().HasData(
            new MissionActivity
            {
                Id = 1,
                MissionId = 1,
                Step = 1,
                ActivityCode = "EVACUATE",
                ActivityType = "Evacuation",
                Description = "Di tản người dân khỏi vùng ngập",
                Target = "{\"location\": \"Khu vực A\", \"count\": 20}",
                TargetLocation = new Point(106.7009, 10.7769) { SRID = 4326 },
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
                Description = "Phân phát thực phẩm và nước",
                Target = "{\"items\": [\"food\", \"water\"], \"count\": 100}",
                TargetLocation = new Point(106.7218, 10.7380) { SRID = 4326 },
                Status = "Planned",
                AssignedAt = now,
                LastDecisionBy = SeedConstants.AdminUserId
            }
        );
    }

    private static void SeedMissionItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MissionItem>().HasData(
            new MissionItem
            {
                Id = 1,
                ReliefItemId = 1,
                MissionId = 1,
                RequiredQuantity = 100,
                AllocatedQuantity = 80,
                SourceDepotId = 1
            },
            new MissionItem
            {
                Id = 2,
                ReliefItemId = 2,
                MissionId = 2,
                RequiredQuantity = 50,
                AllocatedQuantity = 50,
                SourceDepotId = 2
            }
        );
    }

    private static void SeedConversations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>().HasData(
            new Conversation
            {
                Id = 1,
                MissionId = 1
            },
            new Conversation
            {
                Id = 2,
                MissionId = 2
            }
        );
    }

    private static void SeedConversationParticipants(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ConversationParticipant>().HasData(
            new ConversationParticipant
            {
                Id = 1,
                ConversationId = 1,
                UserId = SeedConstants.AdminUserId,
                RoleInConversation = "Admin",
                JoinedAt = now
            },
            new ConversationParticipant
            {
                Id = 2,
                ConversationId = 2,
                UserId = SeedConstants.CoordinatorUserId,
                RoleInConversation = "Coordinator",
                JoinedAt = now
            }
        );
    }

    private static void SeedMessages(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Message>().HasData(
            new Message
            {
                Id = 1,
                ConversationId = 1,
                SenderId = SeedConstants.AdminUserId,
                Content = "Bắt đầu nhiệm vụ cứu hộ",
                CreatedAt = now
            },
            new Message
            {
                Id = 2,
                ConversationId = 2,
                SenderId = SeedConstants.CoordinatorUserId,
                Content = "Đã chuẩn bị xong vật tư cứu trợ",
                CreatedAt = now
            }
        );
    }
}