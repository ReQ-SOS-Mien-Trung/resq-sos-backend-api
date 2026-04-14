using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Mappers.Operations;

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
            // Mission 1: Rescue in Dà N?ng (Cluster 2) - Ðang di?n ra (Scenario 3)
            new Mission
            {
                Id = 1,
                ClusterId = 2,
                MissionType = "Rescue",
                PriorityScore = 10.0,
                Status = MissionMapper.ToDbString(MissionStatus.OnGoing),
                StartTime = now,
                ExpectedEndTime = now.AddHours(6),
                CreatedAt = now,
                CreatedById = SeedConstants.CoordinatorUserId
            },
            // Mission 3: Rescue Phong Ði?n (Cluster 4) - Ðã hoàn thành (Scenario 4)
            new Mission
            {
                Id = 3,
                ClusterId = 4,
                MissionType = "Rescue",
                PriorityScore = 8.5,
                Status = MissionMapper.ToDbString(MissionStatus.Completed),
                StartTime = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                ExpectedEndTime = new DateTime(2026, 3, 1, 14, 0, 0, DateTimeKind.Utc),
                IsCompleted = true,
                CompletedAt = new DateTime(2026, 3, 1, 13, 30, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 1, 7, 45, 0, DateTimeKind.Utc),
                CreatedById = SeedConstants.CoordinatorUserId
            },
            // Mission 4: Relief Phong Ði?n - hoàn thành, có pickup v?t ph?m t?i kho Hu?
            new Mission
            {
                Id = 4,
                ClusterId = 4,
                PreviousMissionId = 3,
                MissionType = "Relief",
                PriorityScore = 7.8,
                Status = MissionMapper.ToDbString(MissionStatus.Completed),
                StartTime = new DateTime(2026, 3, 5, 7, 0, 0, DateTimeKind.Utc),
                ExpectedEndTime = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc),
                IsCompleted = true,
                CompletedAt = new DateTime(2026, 3, 5, 11, 30, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 5, 6, 45, 0, DateTimeKind.Utc),
                CreatedById = SeedConstants.CoordinatorUserId
            },
            // Mission 5: Relief Phong Ði?n - dang di?n ra, có pickup t?i kho Hu? d? test upcoming pickups
            new Mission
            {
                Id = 5,
                ClusterId = 4,
                PreviousMissionId = 4,
                MissionType = "Relief",
                PriorityScore = 8.2,
                Status = MissionMapper.ToDbString(MissionStatus.OnGoing),
                StartTime = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc),
                ExpectedEndTime = new DateTime(2026, 3, 20, 15, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 20, 8, 30, 0, DateTimeKind.Utc),
                CreatedById = SeedConstants.CoordinatorUserId
            },
            // Mission 6: Relief Phong Ði?n - hoàn thành, có c? v?t ph?m tiêu hao L?N thi?t b? tái s? d?ng (áo phao)
            new Mission
            {
                Id = 6,
                ClusterId = 4,
                PreviousMissionId = 5,
                MissionType = "Relief",
                PriorityScore = 7.5,
                Status = MissionMapper.ToDbString(MissionStatus.Completed),
                StartTime = new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc),
                ExpectedEndTime = new DateTime(2026, 3, 8, 15, 0, 0, DateTimeKind.Utc),
                IsCompleted = true,
                CompletedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 8, 6, 45, 0, DateTimeKind.Utc),
                CreatedById = SeedConstants.CoordinatorUserId
            }
        );
    }

    private static void SeedMissionActivities(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 15, 0, DateTimeKind.Utc);
        var pickupHistoryItems = JsonSerializer.Serialize(new[]
        {
            new { ItemId = 1, ItemName = "Mì tôm", Quantity = 120, Unit = "gói" },
            new { ItemId = 2, ItemName = "Nu?c tinh khi?t", Quantity = 240, Unit = "chai" },
            new { ItemId = 3, ItemName = "Thu?c h? s?t Paracetamol 500mg", Quantity = 300, Unit = "viên" }
        });
        var upcomingPickupItems = JsonSerializer.Serialize(new[]
        {
            new { ItemId = 1, ItemName = "Mì tôm", Quantity = 80, Unit = "gói" },
            new { ItemId = 2, ItemName = "Nu?c tinh khi?t", Quantity = 160, Unit = "chai" },
            new { ItemId = 8, ItemName = "Luong khô", Quantity = 120, Unit = "thanh" }
        });
        // Activity 8: RETURN_SUPPLIES - tr? v?t ph?m tiêu hao du th?a v? kho Hu? sau Mission 5
        // â–º Test endpoint: POST /operations/missions/5/activities/8/confirm-return
        // ? Ðang nh?p: manager@resq.vn / Manager@123 (qu?n lý kho Hu? - DepotId=1)
        // ? Ch? consumable: mì tôm x60 + nu?c x80 + thu?c x120
        var returnSuppliesItems = JsonSerializer.Serialize(new[]
        {
            new { ItemId = 1, ItemName = "Mì tôm",                         Quantity = 60,  Unit = "gói"  },
            new { ItemId = 2, ItemName = "Nu?c tinh khi?t",                 Quantity = 80,  Unit = "chai" },
            new { ItemId = 3, ItemName = "Thu?c h? s?t Paracetamol 500mg", Quantity = 120, Unit = "viên" }
        });
        // Activity 9: RETURN_SUPPLIES hoàn thành - tr? v?t ph?m tiêu hao du th?a v? kho Hu? sau Mission 4
        var returnConsumableHistoryItems = JsonSerializer.Serialize(new[]
        {
            new { ItemId = 1, ItemName = "Mì tôm",                         Quantity = 50,  Unit = "gói"  },
            new { ItemId = 3, ItemName = "Thu?c h? s?t Paracetamol 500mg", Quantity = 100, Unit = "viên" }
        });
        // Activity 10: COLLECT_SUPPLIES cho Mission 6 - consumable + reusable (áo phao)
        var mission6PickupItems = JsonSerializer.Serialize(new object[]
        {
            new { ItemId = 1, ItemName = "Mì tôm",            Quantity = 100, Unit = "gói"   },
            new { ItemId = 6, ItemName = "Chan ?m gi? nhi?t", Quantity = 50,  Unit = "chi?c" },
            new
            {
                ItemId = 4, ItemName = "Áo phao c?u sinh", Quantity = 3, Unit = "chi?c",
                ExpectedReturnUnits = new[]
                {
                    new { ReusableItemId = 1, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-001", Condition = "Good" },
                    new { ReusableItemId = 2, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-002", Condition = "Good" },
                    new { ReusableItemId = 3, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-003", Condition = "Fair" }
                }
            }
        });
        // Activity 11: RETURN_SUPPLIES cho Mission 6 - tr? consumable du th?a + toàn b? áo phao
        var mission6ReturnItems = JsonSerializer.Serialize(new object[]
        {
            new { ItemId = 1, ItemName = "Mì tôm",            Quantity = 30, Unit = "gói"   },
            new { ItemId = 6, ItemName = "Chan ?m gi? nhi?t", Quantity = 8,  Unit = "chi?c" },
            new
            {
                ItemId = 4, ItemName = "Áo phao c?u sinh", Quantity = 3, Unit = "chi?c",
                ExpectedReturnUnits = new[]
                {
                    new { ReusableItemId = 1, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-001", Condition = "Good" },
                    new { ReusableItemId = 2, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-002", Condition = "Good" },
                    new { ReusableItemId = 3, ItemModelId = 4, ItemName = "Áo phao c?u sinh", SerialNumber = "D1-R004-003", Condition = "Fair" }
                }
            }
        });

        modelBuilder.Entity<MissionActivity>().HasData(
            new MissionActivity
            {
                Id = 1,
                MissionId = 1,
                Step = 1,
                ActivityType = "EVACUATE",
                Description = "Ti?p c?n khu v?c ng?p sâu L? Th?y, h? tr? y t? và di t?n.",
                Target = "{\"location\": \"Xã An Th?y\", \"count\": 30}",
                TargetLocation = new Point(106.7865, 17.2140) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.OnGoing),
                AssignedAt = now,
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 1 // Bi?t d?i Ca nô Hà Tinh
            },
            // Activity 3: H? tr? y t? cho Mission 1 (bu?c 2, chua b?t d?u)
            new MissionActivity
            {
                Id = 3,
                MissionId = 1,
                Step = 2,
                ActivityType = "MEDICAL_AID",
                Description = "So c?u và h? tr? y t? t?i ch?, uu tiên c? bà 82t và ph? n? mang thai.",
                Target = "{\"location\": \"Khu v?c ng?p Hu?\", \"count\": 9}",
                TargetLocation = new Point(107.568, 16.455) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Planned),
                AssignedAt = now,
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 1
            },
            // Activity 4: Di t?n Mission 3 Phong Ði?n (Succeed)
            new MissionActivity
            {
                Id = 4,
                MissionId = 3,
                Step = 1,
                ActivityType = "EVACUATE",
                Description = "Di t?n n?n nhân ra kh?i vùng ng?p Phong Ði?n, uu tiên ngu?i già và tr? em.",
                Target = "{\"location\": \"Phong Ði?n, Hu?\", \"count\": 10}",
                TargetLocation = new Point(107.582, 16.465) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 1, 8, 5, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                MissionTeamId = 2,
                SosRequestId = 7
            },
            // Activity 5: H? tr? y t? Mission 3 Phong Ði?n (Succeed)
            new MissionActivity
            {
                Id = 5,
                MissionId = 3,
                Step = 2,
                ActivityType = "MEDICAL_AID",
                Description = "So c?u và x? lý y t? t?i hi?n tru?ng, chuy?n ca n?ng lên tuy?n trên.",
                Target = "{\"location\": \"Phong Ði?n, Hu?\", \"count\": 3}",
                TargetLocation = new Point(107.584, 16.467) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 1, 8, 10, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                MissionTeamId = 2,
                SosRequestId = 8
            },
            // Activity 6: Pickup supplies cho Mission 4 t?i kho Hu? (Succeed)
            new MissionActivity
            {
                Id = 6,
                MissionId = 4,
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                Description = "Ð?i v?n chuy?n d?n kho Hu? d? nh?n v?t ph?m c?u tr? tru?c khi di phân ph?i.",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"pickup_supplies\"}",
                Items = pickupHistoryItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 5, 7, 10, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 5, 7, 55, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                MissionTeamId = 3,
                Priority = "High",
                EstimatedTime = 45,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
            },
            // Activity 7: Pickup supplies cho Mission 5 t?i kho Hu? (OnGoing)
            new MissionActivity
            {
                Id = 7,
                MissionId = 5,
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                Description = "Ð?i v?n chuy?n dang l?y hàng t?i kho Hu? d? ch? d?n khu v?c so tán.",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"pickup_supplies\"}",
                Items = upcomingPickupItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.OnGoing),
                AssignedAt = new DateTime(2026, 3, 20, 9, 15, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 4,
                Priority = "Medium",
                EstimatedTime = 30,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
            },
            // Activity 8: RETURN_SUPPLIES cho Mission 5 t?i kho Hu? (PendingConfirmation)
            // ? Dùng d? test: POST /operations/missions/5/activities/8/confirm-return
            // ? Login: manager@resq.vn / Manager@123
            new MissionActivity
            {
                Id = 8,
                MissionId = 5,
                Step = 2,
                ActivityType = "RETURN_SUPPLIES",
                Description = "Hoàn t?t nhi?m v?, tr? v?t ph?m tiêu hao du th?a v? kho Hu?. Tr?: Mì tôm x60 + Nu?c x80 + Thu?c x120.",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"return_supplies\"}",
                Items = returnSuppliesItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.PendingConfirmation),
                AssignedAt = new DateTime(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                MissionTeamId = 4,
                Priority = "Medium",
                EstimatedTime = 30,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
            },
            // Activity 9: RETURN_SUPPLIES hoàn thành cho Mission 4 t?i kho Hu? (Succeed)
            // ? L?ch s? tr? v?t ph?m tiêu hao (consumable only) du th?a sau phân ph?i Phong Ði?n
            new MissionActivity
            {
                Id = 9,
                MissionId = 4,
                Step = 2,
                ActivityType = "RETURN_SUPPLIES",
                Description = "Tr? l?i v?t ph?m tiêu hao du th?a sau khi k?t thúc phân ph?i t?i Phong Ði?n. Tr?: Mì tôm x50 + Thu?c h? s?t x100.",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"return_supplies\"}",
                Items = returnConsumableHistoryItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 5, 11, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 5, 11, 30, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                MissionTeamId = 3,
                Priority = "Low",
                EstimatedTime = 30,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
            },
            // Activity 10: COLLECT_SUPPLIES cho Mission 6 t?i kho Hu? (Succeed)
            // ? L?ch s? l?y hàng có C? consumable (mì tôm, chan ?m) L?N reusable (áo phao x3)
            new MissionActivity
            {
                Id = 10,
                MissionId = 6,
                Step = 1,
                ActivityType = "COLLECT_SUPPLIES",
                Description = "Ð?i v?n chuy?n d?n kho Hu? nh?n v?t ph?m c?u tr? g?m: Mì tôm x100 + Chan ?m x50 + Áo phao c?u sinh x3.",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"pickup_supplies\"}",
                Items = mission6PickupItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 8, 7, 10, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                MissionTeamId = 5,
                Priority = "High",
                EstimatedTime = 45,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
            },
            // Activity 11: RETURN_SUPPLIES cho Mission 6 t?i kho Hu? (Succeed)
            // ? L?ch s? tr? hàng có C? consumable du th?a L?N reusable (áo phao x3 tr? l?i d?y d?)
            new MissionActivity
            {
                Id = 11,
                MissionId = 6,
                Step = 2,
                ActivityType = "RETURN_SUPPLIES",
                Description = "Hoàn t?t nhi?m v?, tr? v? kho Hu?: Mì tôm du x30 + Chan ?m du x8 + Áo phao c?u sinh x3 (d?y d?).",
                Target = "{\"location\":\"Kho Hu?\",\"purpose\":\"return_supplies\"}",
                Items = mission6ReturnItems,
                TargetLocation = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 },
                Status = MissionActivityMapper.ToDbString(MissionActivityStatus.Succeed),
                AssignedAt = new DateTime(2026, 3, 8, 12, 30, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc),
                LastDecisionBy = SeedConstants.CoordinatorUserId,
                CompletedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                MissionTeamId = 5,
                Priority = "Medium",
                EstimatedTime = 30,
                DepotId = 1,
                DepotName = "U? Ban MTTQVN T?nh Th?a Thiên Hu?",
                DepotAddress = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?"
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
            }
        );
    }

    private static void SeedMissionTeams(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<MissionTeam>().HasData(
            // MissionTeam 1: RescueTeam 4 (Bi?t d?i Ca nô Hà Tinh) - Mission 1 dang di?n ra
            new MissionTeam
            {
                Id = 1,
                MissionId = 1,
                RescuerTeamId = 4,
                TeamType = "Rescue",
                Status = "InProgress",
                AssignedAt = now,
                CreatedAt = now,
                Note = "Ð?i dang ti?p c?n khu v?c ng?p sâu L? Th?y"
            },
            // MissionTeam 2: RescueTeam 2 (Ð?i Y t? Hu?) - Mission 3 dã hoàn thành và dã n?p báo cáo
            new MissionTeam
            {
                Id = 2,
                MissionId = 3,
                RescuerTeamId = 2,
                TeamType = "Medical",
                Status = "Reported",
                AssignedAt = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc),
                Note = "Ð?i y t? Hu? hoàn thành nhi?m v? t?i Phong Ði?n"
            },
            new MissionTeam
            {
                Id = 3,
                MissionId = 4,
                RescuerTeamId = 3,
                TeamType = "Transportation",
                Status = "Reported",
                AssignedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc),
                Note = "Ð?i v?n chuy?n Hu? dã hoàn thành l?y hàng t?i kho Hu?"
            },
            new MissionTeam
            {
                Id = 4,
                MissionId = 5,
                RescuerTeamId = 3,
                TeamType = "Transportation",
                Status = "InProgress",
                AssignedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc),
                Note = "Ð?i dang l?y hàng t?i kho Hu? cho d?t c?u tr? m?i"
            },
            // MissionTeam 5: RescueTeam 3 (Ð?i v?n chuy?n Hu?) - Mission 6 dã hoàn thành có c? consumable + reusable
            new MissionTeam
            {
                Id = 5,
                MissionId = 6,
                RescuerTeamId = 3,
                TeamType = "Transportation",
                Status = "Reported",
                AssignedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc),
                Note = "Ð?i v?n chuy?n hoàn thành nhi?m v?, dã tr? d? áo phao và v?t ph?m du th?a"
            }
        );
    }

    private static void SeedMissionTeamMembers(ModelBuilder modelBuilder)
    {
        var now1 = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc);   // Mission 1 join time
        var now3 = new DateTime(2026, 3, 1, 7, 50, 0, DateTimeKind.Utc);    // Mission 3 join time

        modelBuilder.Entity<MissionTeamMember>().HasData(
            // MissionTeam 1 members (RescueTeam 4: Bi?t d?i Ca nô Hà Tinh)
            new MissionTeamMember { Id = 1, MissionTeamId = 1, RescuerId = SeedConstants.RescuerUserId, RoleInTeam = "Leader", JoinedAt = now1 },
            new MissionTeamMember { Id = 2, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330020"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 3, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330021"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 4, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330022"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 5, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330023"), RoleInTeam = "Member", JoinedAt = now1 },
            new MissionTeamMember { Id = 6, MissionTeamId = 1, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330024"), RoleInTeam = "Member", JoinedAt = now1 },
            // MissionTeam 2 members (RescueTeam 2: Ð?i Ph?n ?ng nhanh Y t? Hu?)
            new MissionTeamMember { Id = 7, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330007"), RoleInTeam = "Leader", JoinedAt = now3 },
            new MissionTeamMember { Id = 8, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330008"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 9, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330009"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 10, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330010"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 11, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330011"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 12, MissionTeamId = 2, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330012"), RoleInTeam = "Member", JoinedAt = now3 },
            new MissionTeamMember { Id = 13, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330013"), RoleInTeam = "Leader", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 14, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330014"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 15, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330015"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 16, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330016"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 17, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330017"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 18, MissionTeamId = 3, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330018"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 5, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 19, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330013"), RoleInTeam = "Leader", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 20, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330014"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 21, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330015"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 22, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330016"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 23, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330017"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 24, MissionTeamId = 4, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330018"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc) },
            // MissionTeam 5 members - cùng d?i v?n chuy?n Hu? (RescueTeam 3), Mission 6
            new MissionTeamMember { Id = 25, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330013"), RoleInTeam = "Leader", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 26, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330014"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 27, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330015"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 28, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330016"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 29, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330017"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) },
            new MissionTeamMember { Id = 30, MissionTeamId = 5, RescuerId = Guid.Parse("33333333-3333-3333-3333-333333330018"), RoleInTeam = "Member", JoinedAt = new DateTime(2026, 3, 8, 6, 55, 0, DateTimeKind.Utc) }
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
                TeamSummary = "Ð?i hoàn thành nhi?m v?: di t?n 10 ngu?i, so c?u 3 ngu?i b? thuong.",
                TeamNote = "Ði?u ki?n du?ng b? khó khan nhung hoàn thành dúng ti?n d?.",
                ResultJson = "{\"rescued\":10,\"treated\":3,\"referred\":1}",
                StartedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 3, 1, 13, 20, 0, DateTimeKind.Utc),
                SubmittedAt = submitted,
                SubmittedBy = Guid.Parse("33333333-3333-3333-3333-333333330007"),
                CreatedAt = new DateTime(2026, 3, 1, 13, 0, 0, DateTimeKind.Utc),
                UpdatedAt = submitted
            },
            new MissionTeamReport
            {
                Id = 2,
                MissionTeamId = 3,
                ReportStatus = "Submitted",
                TeamSummary = "Ð?i v?n chuy?n dã hoàn t?t vi?c nh?n hàng t?i kho Hu? và bàn giao cho tuy?n ti?p theo.",
                TeamNote = "Hoàn thành l?y hàng dúng s? lu?ng theo k? ho?ch.",
                ResultJson = "{\"pickedUpItemTypes\":3,\"pickupDepotId\":1}",
                StartedAt = new DateTime(2026, 3, 5, 7, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 3, 5, 8, 10, 0, DateTimeKind.Utc),
                SubmittedAt = new DateTime(2026, 3, 5, 8, 15, 0, DateTimeKind.Utc),
                SubmittedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                CreatedAt = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 5, 8, 15, 0, DateTimeKind.Utc)
            },
            // MissionTeamReport 3: Báo cáo cho MissionTeam 5 (Mission 6 - consumable + reusable)
            new MissionTeamReport
            {
                Id = 3,
                MissionTeamId = 5,
                ReportStatus = "Submitted",
                TeamSummary = "Ð?i v?n chuy?n hoàn thành nhi?m v? Mission 6: nh?n và tr? d?y d? mì tôm, chan ?m và 3 áo phao c?u sinh.",
                TeamNote = "T?t c? áo phao tr? v? dúng tr?ng thái, v?t ph?m tiêu hao du th?a du?c hoàn kho.",
                ResultJson = "{\"pickedUpItemTypes\":3,\"returnedReusableUnits\":3,\"pickupDepotId\":1}",
                StartedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 3, 8, 13, 25, 0, DateTimeKind.Utc),
                SubmittedAt = new DateTime(2026, 3, 8, 13, 30, 0, DateTimeKind.Utc),
                SubmittedBy = Guid.Parse("33333333-3333-3333-3333-333333330013"),
                CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 8, 13, 30, 0, DateTimeKind.Utc)
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
                ActivityType = "EVACUATE",
                ExecutionStatus = "Succeed",
                Summary = "Di t?n thành công 10 ngu?i ra di?m t?p k?t an toàn.",
                CreatedAt = created,
                UpdatedAt = created
            },
            new MissionActivityReport
            {
                Id = 2,
                MissionTeamReportId = 1,
                MissionActivityId = 5,
                ActivityType = "MEDICAL_AID",
                ExecutionStatus = "Succeed",
                Summary = "So c?u 3 ngu?i b? thuong nh?, chuy?n 1 ca n?ng lên tuy?n trên.",
                CreatedAt = created.AddMinutes(5),
                UpdatedAt = created.AddMinutes(5)
            },
            new MissionActivityReport
            {
                Id = 3,
                MissionTeamReportId = 2,
                MissionActivityId = 6,
                ActivityType = "COLLECT_SUPPLIES",
                ExecutionStatus = "Succeed",
                Summary = "Ðã nh?n d? mì tôm, nu?c u?ng và thu?c t?i kho Hu? tru?c khi xu?t phát.",
                CreatedAt = new DateTime(2026, 3, 5, 8, 5, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 5, 8, 5, 0, DateTimeKind.Utc)
            },
            new MissionActivityReport
            {
                Id = 4,
                MissionTeamReportId = 2,
                MissionActivityId = 9,
                ActivityType = "RETURN_SUPPLIES",
                ExecutionStatus = "Succeed",
                Summary = "Tr? l?i 50 gói mì tôm và 100 viên thu?c h? s?t du th?a v? kho Hu? sau khi hoàn thành phân ph?i.",
                CreatedAt = new DateTime(2026, 3, 5, 11, 35, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 5, 11, 35, 0, DateTimeKind.Utc)
            },
            // Reports 5 & 6: Mission 6 - COLLECT + RETURN có c? consumable và reusable
            new MissionActivityReport
            {
                Id = 5,
                MissionTeamReportId = 3,
                MissionActivityId = 10,
                ActivityType = "COLLECT_SUPPLIES",
                ExecutionStatus = "Succeed",
                Summary = "Nh?n d? mì tôm x100, chan ?m x50 và 3 áo phao c?u sinh (D1-R004-001/002/003) t?i kho Hu?.",
                CreatedAt = new DateTime(2026, 3, 8, 8, 5, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 8, 8, 5, 0, DateTimeKind.Utc)
            },
            new MissionActivityReport
            {
                Id = 6,
                MissionTeamReportId = 3,
                MissionActivityId = 11,
                ActivityType = "RETURN_SUPPLIES",
                ExecutionStatus = "Succeed",
                Summary = "Tr? l?i kho Hu?: mì tôm du x30, chan ?m du x8 và d?y d? 3 áo phao c?u sinh còn t?t.",
                CreatedAt = new DateTime(2026, 3, 8, 13, 10, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 8, 13, 10, 0, DateTimeKind.Utc)
            }
        );
    }

    private static void SeedTeamIncidents(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 10, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<TeamIncident>().HasData(
            // S? c? 1: Thuy?n b? h?ng d?ng co khi ti?p c?n khu v?c ng?p
            new TeamIncident
            {
                Id = 1,
                MissionTeamId = 1,
                IncidentScope = TeamIncidentScope.Mission.ToString(),
                Location = new Point(106.7870, 17.2145) { SRID = 4326 },
                Description = "Thuy?n c?u h? b? h?ng d?ng co khi dang ti?p c?n khu v?c ng?p sâu t?i xã An Th?y. Ð?i dang ch? h? tr?.",
                Status = TeamIncidentStatus.Reported.ToString(),
                ReportedBy = SeedConstants.RescuerUserId,
                ReportedAt = now
            },
            // S? c? 2: M?t thành viên d?i c?u h? b? thuong nh?
            new TeamIncident
            {
                Id = 2,
                MissionTeamId = 1,
                IncidentScope = TeamIncidentScope.Mission.ToString(),
                Location = new Point(106.7860, 17.2138) { SRID = 4326 },
                Description = "M?t thành viên d?i c?u h? b? tru?t chân và b? thuong nh? ? chân khi di chuy?n qua khu v?c bùn l?y.",
                Status = TeamIncidentStatus.InProgress.ToString(),
                ReportedBy = SeedConstants.RescuerUserId,
                ReportedAt = now.AddMinutes(30)
            },
            // S? c? 3: M?t liên l?c t?m th?i v?i trung tâm ch? huy
            new TeamIncident
            {
                Id = 3,
                MissionTeamId = 1,
                IncidentScope = TeamIncidentScope.Mission.ToString(),
                Location = new Point(106.7855, 17.2150) { SRID = 4326 },
                Description = "Ð?i m?t liên l?c v?i trung tâm ch? huy trong 15 phút do sóng y?u t?i khu v?c vùng trung.",
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
            // Conversation 3: Mission 3 (Phong Ði?n, dã hoàn thành)
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
                Content = "Ð?i dã ti?p c?n du?c d?u làng. Ðang s? d?ng v?t ph?m y t? d? so c?u ngu?i b? thuong.",
                CreatedAt = now
            }
        );
    }
}
