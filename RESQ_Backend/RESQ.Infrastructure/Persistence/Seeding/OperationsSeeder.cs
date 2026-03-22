using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class OperationsSeeder
{
    public static void SeedOperations(this ModelBuilder modelBuilder)
    {
        SeedMissions(modelBuilder);
        SeedMissionActivities(modelBuilder);
        SeedMissionItems(modelBuilder);
        SeedMissionTeams(modelBuilder);
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
                Status = MissionStatus.OnGoing.ToString(),
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
                Status = MissionStatus.Planned.ToString(),
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
                Status = MissionActivityStatus.OnGoing.ToString(),
                AssignedAt = now,
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 1 // Biệt đội Ca nô Hà Tĩnh
            },
            new MissionActivity
            {
                Id = 2,
                MissionId = 2,
                Step = 1,
                ActivityCode = "DISTRIBUTE",
                ActivityType = "Distribution",
                Description = "Phân phát lương thực cứu trợ (gạo, mì) tại vùng lũ TT-Huế.",
                Target = "{\"items\": [\"rice\", \"food\"], \"count\": 200}",
                TargetLocation = new Point(107.5680, 16.4546) { SRID = 4326 },
                Status = MissionActivityStatus.Planned.ToString(),
                AssignedAt = now,
                LastDecisionBy = SeedConstants.AdminUserId
            }
        );
    }

    private static void SeedMissionItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MissionItem>().HasData(
            // Mission 1 (Rescue): Needs Medical/Rescue Kits. 
            // ReliefItem ID 2 (First Aid/Medical)
            new MissionItem
            {
                Id = 1,
                ItemModelId = 2, 
                MissionId = 1, 
                RequiredQuantity = 20,
                AllocatedQuantity = 20,
                SourceDepotId = 2 // Le Thuy Depot
            },
            // Mission 2 (Relief): Needs Food.
            // ReliefItem ID 1 (Rice/Food)
            new MissionItem
            {
                Id = 2,
                ItemModelId = 1, 
                MissionId = 2, 
                RequiredQuantity = 100,
                AllocatedQuantity = 100,
                SourceDepotId = 1 // Hue Depot
            }
        );
    }

    private static void SeedMissionTeams(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionTeam>().HasData(
            // Team 4 (Biệt đội Ca nô Hà Tĩnh) được giao Mission 1 (Rescue — để test luồng nhận nhiệm vụ của rescuer)
            new MissionTeam
            {
                Id = 1,
                MissionId = 1,
                RescuerTeamId = 4,
                TeamType = "Rescue",
                Status = "Assigned",
                AssignedAt = now,
                CreatedAt = now,
                Note = "Đội được giao nhiệm vụ cứu hộ tại Lệ Thủy"
            }
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
                Content = "Đã xuất kho 100 thùng mì tôm từ kho MTTQ Huế, xe đang di chuyển.",
                CreatedAt = now
            }
        );
    }
}
