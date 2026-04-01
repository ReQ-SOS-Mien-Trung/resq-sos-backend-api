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
        SeedMissionTeamMembers(modelBuilder);
        SeedMissionTeamReports(modelBuilder);
        SeedMissionActivityReports(modelBuilder);
        SeedTeamIncidents(modelBuilder);
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
            },
            // Mission 3: Rescue Phong Điền (Cluster 4) — Đã hoàn thành (Scenario 4)
            new Mission
            {
                Id = 3,
                ClusterId = 4,
                MissionType = "Rescue",
                PriorityScore = 8.5,
                Status = MissionStatus.Completed.ToString(),
                StartTime = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                ExpectedEndTime = new DateTime(2026, 3, 1, 14, 0, 0, DateTimeKind.Utc),
                IsCompleted = true,
                CompletedAt = new DateTime(2026, 3, 1, 13, 30, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 1, 7, 45, 0, DateTimeKind.Utc),
                CreatedById = SeedConstants.CoordinatorUserId
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
            },
            // Activity 3: Hỗ trợ y tế cho Mission 1 (bước 2, chưa bắt đầu)
            new MissionActivity
            {
                Id = 3,
                MissionId = 1,
                Step = 2,
                ActivityCode = "MEDICAL_SUPPORT",
                ActivityType = "Medical",
                Description = "Sơ cứu và hỗ trợ y tế tại chỗ, ưu tiên cụ bà 82t và phụ nữ mang thai.",
                Target = "{\"location\": \"Khu vực ngập Huế\", \"count\": 9}",
                TargetLocation = new Point(107.568, 16.455) { SRID = 4326 },
                Status = MissionActivityStatus.Planned.ToString(),
                AssignedAt = now,
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 1
            },
            // Activity 4: Di tản Mission 3 Phong Điền (Succeed)
            new MissionActivity
            {
                Id = 4,
                MissionId = 3,
                Step = 1,
                ActivityCode = "EVACUATE",
                ActivityType = "Evacuation",
                Description = "Di tản nạn nhân ra khỏi vùng ngập Phong Điền, ưu tiên người già và trẻ em.",
                Target = "{\"location\": \"Phong Điền, Huế\", \"count\": 10}",
                TargetLocation = new Point(107.582, 16.465) { SRID = 4326 },
                Status = MissionActivityStatus.Succeed.ToString(),
                AssignedAt = new DateTime(2026, 3, 1, 8, 5, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                MissionTeamId = 2,
                SosRequestId = 7
            },
            // Activity 5: Hỗ trợ y tế Mission 3 Phong Điền (Succeed)
            new MissionActivity
            {
                Id = 5,
                MissionId = 3,
                Step = 2,
                ActivityCode = "MEDICAL_SUPPORT",
                ActivityType = "Medical",
                Description = "Sơ cứu và xử lý y tế tại hiện trường, chuyển ca nặng lên tuyến trên.",
                Target = "{\"location\": \"Phong Điền, Huế\", \"count\": 3}",
                TargetLocation = new Point(107.584, 16.467) { SRID = 4326 },
                Status = MissionActivityStatus.Succeed.ToString(),
                AssignedAt = new DateTime(2026, 3, 1, 8, 10, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                MissionTeamId = 2,
                SosRequestId = 8
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
            // MissionTeam 1: RescueTeam 4 (Biệt đội Ca nô Hà Tĩnh) — Mission 1 đang diễn ra
            new MissionTeam
            {
                Id = 1,
                MissionId = 1,
                RescuerTeamId = 4,
                TeamType = "Rescue",
                Status = "InProgress",
                AssignedAt = now,
                CreatedAt = now,
                Note = "Đội đang tiếp cận khu vực ngập sâu Lệ Thủy"
            },
            // MissionTeam 2: RescueTeam 2 (Đội Y tế Huế) — Mission 3 đã hoàn thành và đã nộp báo cáo
            new MissionTeam
            {
                Id = 2,
                MissionId = 3,
                RescuerTeamId = 2,
                TeamType = "Medical",
                Status = "Reported",
                AssignedAt = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc),
                Note = "Đội y tế Huế hoàn thành nhiệm vụ tại Phong Điền"
            }
        );
    }

    private static void SeedMissionTeamMembers(ModelBuilder modelBuilder)
    {
        var now1 = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);   // Mission 1 join time
        var now3 = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc);    // Mission 3 join time

        modelBuilder.Entity<MissionTeamMember>().HasData(
            // MissionTeam 1 members (RescueTeam 4: Biệt đội Ca nô Hà Tĩnh)
            new MissionTeamMember { Id = 1, MissionTeamId = 1, RescuerId = SeedConstants.RescuerUserId, RoleInTeam = "Leader", JoinedAt = now1 },
            new MissionTeamMember { Id = 2, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330020"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 3, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330021"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 4, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330022"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 5, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330023"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 6, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330024"), RoleInTeam = "Member", JoinedAt = now1 },
            // MissionTeam 2 members (RescueTeam 2: Đội Phản ứng nhanh Y tế Huế)
            new MissionTeamMember { Id = 7, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330007"), RoleInTeam = "Leader", JoinedAt = now3 },
            new MissionTeamMember { Id = 8, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330008"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 9, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330009"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 10, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330010"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 11, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330011"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 12, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330012"), RoleInTeam = "Member", JoinedAt = now3 }
        );
    }

    private static void SeedMissionTeamReports(ModelBuilder modelBuilder)
    {
        var submitted = new DateTime(2026, 3, 1, 13, 30, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionTeamReport>().HasData(
            new MissionTeamReport
            {
                Id = 1,
                MissionTeamId = 2,
                ReportStatus = "Submitted",
                TeamSummary = "Đội hoàn thành nhiệm vụ: di tản 10 người, sơ cứu 3 người bị thương.",
                TeamNote = "Điều kiện đường bộ khó khăn nhưng hoàn thành đúng tiến độ.",
                ResultJson = "{\"rescued\":10,\"treated\":3,\"referred\":1}",
                StartedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 3, 1, 13, 20, 0, DateTimeKind.Utc),
                SubmittedAt = submitted,
                SubmittedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                CreatedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                UpdatedAt = submitted
            }
        );
    }

    private static void SeedMissionActivityReports(ModelBuilder modelBuilder)
    {
        var created = new DateTime(2026, 3, 1, 13, 5, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionActivityReport>().HasData(
            new MissionActivityReport
            {
                Id = 1,
                MissionTeamReportId = 1,
                MissionActivityId = 4,
                ActivityCode = "EVACUATE",
                ActivityType = "Evacuation",
                ExecutionStatus = "Succeed",
                Summary = "Di tản thành công 10 người ra điểm tập kết an toàn.",
                CreatedAt = created,
                UpdatedAt = created
            },
            new MissionActivityReport
            {
                Id = 2,
                MissionTeamReportId = 1,
                MissionActivityId = 5,
                ActivityCode = "MEDICAL_SUPPORT",
                ActivityType = "Medical",
                ExecutionStatus = "Succeed",
                Summary = "Sơ cứu 3 người bị thương nhẹ, chuyển 1 ca nặng lên tuyến trên.",
                CreatedAt = created.AddMinutes(5),
                UpdatedAt = created.AddMinutes(5)
            }
        );
    }

    private static void SeedTeamIncidents(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 10, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<TeamIncident>().HasData(
            // Sự cố 1: Thuyền bị hỏng động cơ khi tiếp cận khu vực ngập
            new TeamIncident
            {
                Id = 1,
                MissionTeamId = 1,
                Location = new Point(106.7870, 17.2145) { SRID = 4326 },
                Description = "Thuyền cứu hộ bị hỏng động cơ khi đang tiếp cận khu vực ngập sâu tại xã An Thủy. Đội đang chờ hỗ trợ.",
                Status = TeamIncidentStatus.Reported.ToString(),
                ReportedBy = SeedConstants.RescuerUserId,
                ReportedAt = now
            },
            // Sự cố 2: Một thành viên đội cứu hộ bị thương nhẹ
            new TeamIncident
            {
                Id = 2,
                MissionTeamId = 1,
                Location = new Point(106.7860, 17.2138) { SRID = 4326 },
                Description = "Một thành viên đội cứu hộ bị trượt chân và bị thương nhẹ ở chân khi di chuyển qua khu vực bùn lầy.",
                Status = TeamIncidentStatus.Acknowledged.ToString(),
                ReportedBy = SeedConstants.RescuerUserId,
                ReportedAt = now.AddMinutes(30)
            },
            // Sự cố 3: Mất liên lạc tạm thời với trung tâm chỉ huy
            new TeamIncident
            {
                Id = 3,
                MissionTeamId = 1,
                Location = new Point(106.7855, 17.2150) { SRID = 4326 },
                Description = "Đội mất liên lạc với trung tâm chỉ huy trong 15 phút do sóng yếu tại khu vực vùng trũng.",
                Status = TeamIncidentStatus.Resolved.ToString(),
                ReportedBy = SeedConstants.RescuerUserId,
                ReportedAt = now.AddMinutes(45)
            }
        );
    }

    private static void SeedConversations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>().HasData(
            new Conversation { Id = 1, MissionId = 1 },
            new Conversation { Id = 2, MissionId = 2 },
            new Conversation { Id = 3, MissionId = 3 }
        );
    }

    private static void SeedConversationParticipants(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);
        var now3 = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ConversationParticipant>().HasData(
            new ConversationParticipant { Id = 1, ConversationId = 1, UserId = SeedConstants.AdminUserId, RoleInConversation = "Monitor", JoinedAt = now },
            new ConversationParticipant { Id = 2, ConversationId = 1, UserId = SeedConstants.RescuerUserId, RoleInConversation = "Leader", JoinedAt = now },
            new ConversationParticipant { Id = 3, ConversationId = 2, UserId = SeedConstants.CoordinatorUserId, RoleInConversation = "Logistics", JoinedAt = now },
            // Conversation 3: Mission 3 (Phong Điền, đã hoàn thành)
            new ConversationParticipant { Id = 4, ConversationId = 3, UserId = SeedConstants.CoordinatorUserId, RoleInConversation = "Monitor", JoinedAt = now3 },
            new ConversationParticipant { Id = 5, ConversationId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330007"), RoleInConversation = "Leader", JoinedAt = now3 }
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
