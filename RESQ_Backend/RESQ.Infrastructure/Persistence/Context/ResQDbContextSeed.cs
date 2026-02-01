using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext
{
    /*
     * ===============================================
     * SEED DATA - THÔNG TIN ĐĂNG NHẬP
     * ===============================================
     * 
     * User 1 (Admin):
     *   - Username: admin
     *   - Password: Admin@123
     * 
     * User 2 (Coordinator):
     *   - Username: coordinator
     *   - Password: Coordinator@123
     * 
     * User 3 (Rescuer):
     *   - Username: rescuer
     *   - Password: Rescuer@123
     * 
     * User 4 (Manager):
     *   - Username: manager
     *   - Password: Manager@123
     * 
     * User 5 (Victim):
     *   - Username: victim
     *   - Password: Victim@123
     * 
     * ===============================================
     */

    // Pre-hashed passwords using BCrypt with work factor 11
    // Password: Admin@123
    private const string AdminPasswordHash = "$2a$11$ijKjJLUF47vj/JkLAg5C4eOzZ11yQ1ORWquJTBlIOPIIeWTimQCBm";
    // Password: Coordinator@123
    private const string CoordinatorPasswordHash = "$2a$11$tawo9jpZGHHA25NfCF6hUOLYIcgETiaCTvmsM4oOd0VH5mwMkn6.O";
    // Password: Rescuer@123
    private const string RescuerPasswordHash = "$2a$11$RipGftiyzl4tYLZZdLLJ4ufKnogeR8kWp1DeKlpj44eQcWlzNk3.u";
    // Password: Manager@123
    private const string ManagerPasswordHash = "$2a$11$mIi0t6MBeHaLRz8X/EUAvOn0RsbZs4pnJ4weyoVkusnCf2grE45oG";
    // Password: Victim@123
    private const string VictimPasswordHash = "$2a$11$on1XCfJiZ.y.280Rx2rKkOFOPn2UnX42ay7V8pZ2QJUkDW4IbD38O";

    // Fixed GUIDs for Users to maintain relationships
    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CoordinatorUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RescuerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ManagerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid VictimUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedAbilities(modelBuilder);
        SeedUsers(modelBuilder);
        SeedCategories(modelBuilder);
        SeedOrganizations(modelBuilder);
        SeedReliefItems(modelBuilder);
        SeedDepots(modelBuilder);
        SeedDepotManagers(modelBuilder);
        SeedDepotInventories(modelBuilder);
        SeedInventoryLogs(modelBuilder);
        SeedRescueTeams(modelBuilder);
        // SeedUnitMembers(modelBuilder); // Entity removed/changed
        SeedUserAbilities(modelBuilder);
        SeedSosClusters(modelBuilder);
        SeedSosRequests(modelBuilder);
        SeedSosAiAnalyses(modelBuilder);
        SeedMissions(modelBuilder);
        SeedMissionActivities(modelBuilder);
        SeedMissionItems(modelBuilder);
        SeedConversations(modelBuilder);
        SeedConversationParticipants(modelBuilder);
        SeedMessages(modelBuilder);
        SeedNotifications(modelBuilder);
        SeedPrompts(modelBuilder);
        SeedClusterAiAnalyses(modelBuilder);
        SeedActivityAiSuggestions(modelBuilder);
        // SeedActivityHandoverLogs(modelBuilder); // Entity removed/changed
        SeedRescueTeamAiSuggestions(modelBuilder);
        SeedOrganizationReliefItems(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Coordinator" },
            new Role { Id = 3, Name = "Rescuer" },
            new Role { Id = 4, Name = "Manager" },
            new Role { Id = 5, Name = "Victim" }
        );
    }

    private static void SeedAbilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ability>().HasData(
            new Ability { Id = 1, Code = "FIRST_AID", Description = "Khả năng sơ cứu cơ bản" },
            new Ability { Id = 2, Code = "SWIMMING", Description = "Khả năng bơi lội, cứu hộ dưới nước" }
        );
    }

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = AdminUserId,
                RoleId = 1,
                FullName = "Nguyễn Văn Admin",
                Username = "admin",
                Phone = "0901234567",
                Password = AdminPasswordHash,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = CoordinatorUserId,
                RoleId = 2,
                FullName = "Trần Thị Coordinator",
                Username = "coordinator",
                Phone = "0912345678",
                Password = CoordinatorPasswordHash,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = RescuerUserId,
                RoleId = 3,
                FullName = "Lê Văn Rescuer",
                Username = "rescuer",
                Phone = "0923456789",
                Password = RescuerPasswordHash,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = ManagerUserId,
                RoleId = 4,
                FullName = "Phạm Thị Manager",
                Username = "manager",
                Phone = "0934567890",
                Password = ManagerPasswordHash,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = VictimUserId,
                RoleId = 5,
                FullName = "Hoàng Văn Victim",
                Username = "victim",
                Phone = "0945678901",
                Password = VictimPasswordHash,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        modelBuilder.Entity<ItemCategory>().HasData(
            new ItemCategory
            {
                Id = 1,
                Code = "FOOD",
                Name = "Thực phẩm",
                Description = "Các loại thực phẩm cứu trợ",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ItemCategory
            {
                Id = 2,
                Code = "MEDICAL",
                Name = "Y tế",
                Description = "Các vật tư y tế, thuốc men",
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedOrganizations(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Organization>().HasData(
            new Organization
            {
                Id = 1,
                Name = "Hội Chữ Thập Đỏ Việt Nam",
                Phone = "0281234567",
                Email = "contact@redcross.org.vn",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Organization
            {
                Id = 2,
                Name = "Tổ chức Cứu trợ Nhân đạo ABC",
                Phone = "0289876543",
                Email = "info@abc-relief.org",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedReliefItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        modelBuilder.Entity<ReliefItem>().HasData(
            new ReliefItem
            {
                Id = 1,
                CategoryId = 1,
                Name = "Gạo",
                Unit = "kg",
                TargetGroup = "Tất cả",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ReliefItem
            {
                Id = 2,
                CategoryId = 2,
                Name = "Bộ sơ cứu",
                Unit = "bộ",
                TargetGroup = "Tất cả",
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Depot>().HasData(
            new Depot
            {
                Id = 1,
                Name = "Kho cứu trợ Quận 1",
                Address = "123 Nguyễn Huệ, Quận 1, TP.HCM",
                Location = new Point(106.7009, 10.7769) { SRID = 4326 },
                Status = "Active",
                Capacity = 1000,
                CurrentUtilization = 500,
                LastUpdatedAt = now
            },
            new Depot
            {
                Id = 2,
                Name = "Kho cứu trợ Quận 7",
                Address = "456 Nguyễn Văn Linh, Quận 7, TP.HCM",
                Location = new Point(106.7218, 10.7380) { SRID = 4326 },
                Status = "Active",
                Capacity = 2000,
                CurrentUtilization = 800,
                LastUpdatedAt = now
            }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager
            {
                Id = 1,
                DepotId = 1,
                UserId = AdminUserId,
                AssignedAt = now
            },
            new DepotManager
            {
                Id = 2,
                DepotId = 2,
                UserId = CoordinatorUserId,
                AssignedAt = now
            }
        );
    }

    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyInventory>().HasData(
            new DepotSupplyInventory
            {
                Id = 1,
                DepotId = 1,
                ReliefItemId = 1,
                Quantity = 500,
                ReservedQuantity = 100,
                LastStockedAt = now
            },
            new DepotSupplyInventory
            {
                Id = 2,
                DepotId = 2,
                ReliefItemId = 2,
                Quantity = 200,
                ReservedQuantity = 50,
                LastStockedAt = now
            }
        );
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<InventoryLog>().HasData(
            new InventoryLog
            {
                Id = 1,
                DepotSupplyInventoryId = 1,
                ActionType = "Import",
                QuantityChange = 500,
                SourceType = "Donation",
                SourceId = 1,
                PerformedBy = AdminUserId,
                Note = "Nhập kho đợt 1",
                CreatedAt = now
            },
            new InventoryLog
            {
                Id = 2,
                DepotSupplyInventoryId = 2,
                ActionType = "Import",
                QuantityChange = 200,
                SourceType = "Donation",
                SourceId = 2,
                PerformedBy = CoordinatorUserId,
                Note = "Nhập kho đợt 1",
                CreatedAt = now
            }
        );
    }

    private static void SeedRescueTeams(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now2 = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeam>().HasData(
            new RescueTeam
            {
                Id = 1,
                Name = "Đội cứu hộ Alpha",
                Location = new Point(106.7009, 10.7769) { SRID = 4326 },
                Status = "Available",
                MaxMembers = 10,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 2,
                Name = "Đội cứu hộ Beta",
                Location = new Point(106.7218, 10.7380) { SRID = 4326 },
                Status = "OnMission",
                MaxMembers = 8,
                CreatedAt = now2
            }
        );
    }

    private static void SeedUserAbilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAbility>().HasData(
            new UserAbility
            {
                UserId = AdminUserId,
                AbilityId = 1,
                Level = 5
            },
            new UserAbility
            {
                UserId = CoordinatorUserId,
                AbilityId = 2,
                Level = 4
            }
        );
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
                UserId = AdminUserId,
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
                UserId = CoordinatorUserId,
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
                CreatedById = CoordinatorUserId
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
                CreatedById = AdminUserId
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
                LastDecisionBy = CoordinatorUserId
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
                LastDecisionBy = AdminUserId
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
                UserId = AdminUserId,
                RoleInConversation = "Admin",
                JoinedAt = now
            },
            new ConversationParticipant
            {
                Id = 2,
                ConversationId = 2,
                UserId = CoordinatorUserId,
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
                SenderId = AdminUserId,
                Content = "Bắt đầu nhiệm vụ cứu hộ",
                CreatedAt = now
            },
            new Message
            {
                Id = 2,
                ConversationId = 2,
                SenderId = CoordinatorUserId,
                Content = "Đã chuẩn bị xong vật tư cứu trợ",
                CreatedAt = now
            }
        );
    }

    private static void SeedNotifications(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Notification>().HasData(
            new Notification
            {
                Id = 1,
                UserId = AdminUserId,
                Content = "Có yêu cầu cứu hộ mới cần xử lý",
                CreatedAt = now
            },
            new Notification
            {
                Id = 2,
                UserId = CoordinatorUserId,
                Content = "Nhiệm vụ #1 đã được giao cho đội của bạn",
                CreatedAt = now
            }
        );
    }

    private static void SeedPrompts(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        modelBuilder.Entity<Prompt>().HasData(
            new Prompt
            {
                Id = 1,
                Name = "SOS Analysis Prompt",
                Purpose = "Phân tích tin nhắn SOS để trích xuất thông tin",
                SystemPrompt = "Bạn là một AI chuyên phân tích các tin nhắn cầu cứu trong thiên tai...",
                Temperature = 0.3,
                MaxTokens = 1000,
                Version = "v1.0",
                CreatedAt = now
            },
            new Prompt
            {
                Id = 2,
                Name = "Mission Planning Prompt",
                Purpose = "Lập kế hoạch nhiệm vụ cứu trợ",
                SystemPrompt = "Bạn là một AI hỗ trợ lập kế hoạch nhiệm vụ cứu trợ thiên tai...",
                Temperature = 0.5,
                MaxTokens = 2000,
                Version = "v1.0",
                CreatedAt = now
            }
        );
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

    private static void SeedOrganizationReliefItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationReliefItem>().HasData(
            new OrganizationReliefItem
            {
                Id = 1,
                OrganizationId = 1,
                ReliefItemId = 1,
                ReceivedDate = new DateOnly(2024, 1, 1),
                ExpiredDate = new DateOnly(2025, 1, 1),
                Notes = "Quyên góp từ tổ chức Hội Chữ Thập Đỏ"
            },
            new OrganizationReliefItem
            {
                Id = 2,
                OrganizationId = 2,
                ReliefItemId = 2,
                ReceivedDate = new DateOnly(2024, 1, 15),
                ExpiredDate = new DateOnly(2026, 1, 15),
                Notes = "Quyên góp từ tổ chức ABC"
            }
        );
    }
}
