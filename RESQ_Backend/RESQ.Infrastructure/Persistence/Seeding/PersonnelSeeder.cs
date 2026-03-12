using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class PersonnelSeeder
{
    public static void SeedPersonnel(this ModelBuilder modelBuilder)
    {
        SeedAbilityCategories(modelBuilder);
        SeedAbilitySubgroups(modelBuilder);
        SeedAbilities(modelBuilder);
        SeedAssemblyPoints(modelBuilder);
        SeedRescueTeams(modelBuilder);
        SeedUserAbilities(modelBuilder);
    }

    private static void SeedAbilityCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbilityCategory>().HasData(
            new AbilityCategory { Id = 1, Code = "RESCUE", Description = "Kỹ năng cứu hộ" },
            new AbilityCategory { Id = 2, Code = "MEDICAL", Description = "Kỹ năng y tế" },
            new AbilityCategory { Id = 3, Code = "TRANSPORTATION", Description = "Kỹ năng vận chuyển" },
            new AbilityCategory { Id = 4, Code = "EXPERIENCE", Description = "Kinh nghiệm thực tiễn" }
        );
    }

    private static void SeedAbilitySubgroups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbilitySubgroup>().HasData(
            // RESCUE subgroups
            new AbilitySubgroup { Id = 1, Code = "WATER_SKILLS", Description = "Kỹ năng bơi lội", AbilityCategoryId = 1 },
            new AbilitySubgroup { Id = 2, Code = "LIFESAVING_SKILLS", Description = "Kỹ năng cứu người", AbilityCategoryId = 1 },
            new AbilitySubgroup { Id = 3, Code = "HARSH_ENVIRONMENT_RESCUE", Description = "Cứu hộ trong điều kiện khắc nghiệt", AbilityCategoryId = 1 },
            // MEDICAL subgroups
            new AbilitySubgroup { Id = 4, Code = "PROFESSIONAL_MEDICAL", Description = "Y tế chuyên môn", AbilityCategoryId = 2 },
            new AbilitySubgroup { Id = 5, Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản", AbilityCategoryId = 2 },
            new AbilitySubgroup { Id = 6, Code = "EMERGENCY_CARE", Description = "Cấp cứu", AbilityCategoryId = 2 },
            new AbilitySubgroup { Id = 7, Code = "TRAUMA_CARE", Description = "Chấn thương", AbilityCategoryId = 2 },
            // TRANSPORTATION subgroups
            new AbilitySubgroup { Id = 8, Code = "LAND_VEHICLES", Description = "Lái xe cơ giới", AbilityCategoryId = 3 },
            new AbilitySubgroup { Id = 9, Code = "WATER_VEHICLES", Description = "Lái phương tiện thủy", AbilityCategoryId = 3 },
            new AbilitySubgroup { Id = 10, Code = "SPECIALIZED_DRIVING", Description = "Kỹ năng điều khiển đặc biệt", AbilityCategoryId = 3 },
            new AbilitySubgroup { Id = 11, Code = "TRANSPORT_OPERATIONS", Description = "Vận chuyển", AbilityCategoryId = 3 },
            // EXPERIENCE subgroups
            new AbilitySubgroup { Id = 12, Code = "FIELD_EXPERIENCE", Description = "Kinh nghiệm thực tế", AbilityCategoryId = 4 },
            new AbilitySubgroup { Id = 13, Code = "ORGANIZATIONAL_MEMBERSHIP", Description = "Tổ chức", AbilityCategoryId = 4 }
        );
    }

    private static void SeedAbilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ability>().HasData(
            // WATER_SKILLS (subgroup 1)
            new Ability { Id = 1, Code = "BASIC_SWIMMING", Description = "Bơi cơ bản", AbilitySubgroupId = 1 },
            new Ability { Id = 2, Code = "ADVANCED_SWIMMING", Description = "Bơi thành thạo", AbilitySubgroupId = 1 },
            new Ability { Id = 3, Code = "WATER_RESCUE", Description = "Cứu hộ dưới nước", AbilitySubgroupId = 1 },
            new Ability { Id = 4, Code = "DEEP_WATER_MOVEMENT", Description = "Di chuyển trong nước ngập sâu", AbilitySubgroupId = 1 },
            new Ability { Id = 5, Code = "RAPID_WATER_MOVEMENT", Description = "Di chuyển trong dòng nước chảy xiết", AbilitySubgroupId = 1 },
            new Ability { Id = 6, Code = "BASIC_DIVING", Description = "Lặn cơ bản", AbilitySubgroupId = 1 },
            new Ability { Id = 7, Code = "FLOOD_ESCAPE", Description = "Thoát hiểm trong môi trường ngập nước", AbilitySubgroupId = 1 },
            // LIFESAVING_SKILLS (subgroup 2)
            new Ability { Id = 8, Code = "FLOODED_HOUSE_RESCUE", Description = "Cứu người bị mắc kẹt trong nhà ngập", AbilitySubgroupId = 2 },
            new Ability { Id = 9, Code = "ROOFTOP_RESCUE", Description = "Cứu người bị mắc kẹt trên mái nhà", AbilitySubgroupId = 2 },
            new Ability { Id = 10, Code = "VEHICLE_RESCUE", Description = "Cứu người bị kẹt trong phương tiện (xe, ghe)", AbilitySubgroupId = 2 },
            new Ability { Id = 11, Code = "ROPE_RESCUE", Description = "Sử dụng dây thừng cứu hộ", AbilitySubgroupId = 2 },
            new Ability { Id = 12, Code = "LIFE_JACKET_USE", Description = "Sử dụng áo phao, phao cứu sinh", AbilitySubgroupId = 2 },
            // HARSH_ENVIRONMENT_RESCUE (subgroup 3)
            new Ability { Id = 13, Code = "NIGHT_RESCUE", Description = "Cứu hộ ban đêm / tầm nhìn kém", AbilitySubgroupId = 3 },
            new Ability { Id = 14, Code = "STORM_RESCUE", Description = "Cứu hộ trong mưa lớn / bão", AbilitySubgroupId = 3 },
            new Ability { Id = 15, Code = "DEBRIS_RESCUE", Description = "Cứu hộ tại khu vực đổ nát", AbilitySubgroupId = 3 },
            new Ability { Id = 16, Code = "HAZARDOUS_RESCUE", Description = "Cứu hộ trong môi trường nguy hiểm", AbilitySubgroupId = 3 },
            // BASIC_FIRST_AID (subgroup 5)
            new Ability { Id = 17, Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản", AbilitySubgroupId = 5 },
            new Ability { Id = 18, Code = "OPEN_WOUND_CARE", Description = "Sơ cứu vết thương hở", AbilitySubgroupId = 5 },
            new Ability { Id = 19, Code = "BLEEDING_CONTROL", Description = "Cầm máu", AbilitySubgroupId = 5 },
            new Ability { Id = 20, Code = "WOUND_BANDAGING", Description = "Băng bó vết thương", AbilitySubgroupId = 5 },
            new Ability { Id = 21, Code = "MINOR_INJURY_CARE", Description = "Xử lý trầy xước, chấn thương nhẹ", AbilitySubgroupId = 5 },
            new Ability { Id = 22, Code = "MINOR_BURN_CARE", Description = "Xử lý bỏng nhẹ", AbilitySubgroupId = 5 },
            // EMERGENCY_CARE (subgroup 6)
            new Ability { Id = 23, Code = "CPR", Description = "Hồi sức tim phổi (CPR)", AbilitySubgroupId = 6 },
            new Ability { Id = 24, Code = "DROWNING_RESPONSE", Description = "Xử lý đuối nước", AbilitySubgroupId = 6 },
            new Ability { Id = 25, Code = "SHOCK_TREATMENT", Description = "Xử lý sốc", AbilitySubgroupId = 6 },
            new Ability { Id = 26, Code = "HYPOTHERMIA_TREATMENT", Description = "Xử lý hạ thân nhiệt", AbilitySubgroupId = 6 },
            new Ability { Id = 27, Code = "VITAL_SIGNS_MONITORING", Description = "Theo dõi dấu hiệu sinh tồn", AbilitySubgroupId = 6 },
            new Ability { Id = 28, Code = "VICTIM_ASSESSMENT", Description = "Đánh giá mức độ nguy kịch nạn nhân", AbilitySubgroupId = 6 },
            // TRAUMA_CARE (subgroup 7)
            new Ability { Id = 29, Code = "FRACTURE_IMMOBILIZATION", Description = "Cố định gãy xương tạm thời", AbilitySubgroupId = 7 },
            new Ability { Id = 30, Code = "SPINAL_INJURY_CARE", Description = "Xử lý chấn thương cột sống (cơ bản)", AbilitySubgroupId = 7 },
            new Ability { Id = 31, Code = "SAFE_PATIENT_TRANSPORT", Description = "Vận chuyển người bị thương an toàn", AbilitySubgroupId = 7 },
            // PROFESSIONAL_MEDICAL (subgroup 4)
            new Ability { Id = 32, Code = "MEDICAL_STAFF", Description = "Nhân viên y tế", AbilitySubgroupId = 4 },
            new Ability { Id = 33, Code = "NURSE", Description = "Y tá", AbilitySubgroupId = 4 },
            new Ability { Id = 34, Code = "DOCTOR", Description = "Bác sĩ", AbilitySubgroupId = 4 },
            new Ability { Id = 35, Code = "PREHOSPITAL_EMERGENCY", Description = "Cấp cứu tiền viện", AbilitySubgroupId = 4 },
            // LAND_VEHICLES (subgroup 8)
            new Ability { Id = 36, Code = "MOTORCYCLE_DRIVING", Description = "Lái xe máy", AbilitySubgroupId = 8 },
            new Ability { Id = 37, Code = "MOTORCYCLE_FLOOD_DRIVING", Description = "Lái xe máy trong điều kiện ngập nước", AbilitySubgroupId = 8 },
            new Ability { Id = 38, Code = "CAR_DRIVING", Description = "Lái ô tô", AbilitySubgroupId = 8 },
            new Ability { Id = 39, Code = "OFFROAD_DRIVING", Description = "Lái ô tô địa hình", AbilitySubgroupId = 8 },
            // WATER_VEHICLES (subgroup 9)
            new Ability { Id = 40, Code = "ROWBOAT_DRIVING", Description = "Lái ghe", AbilitySubgroupId = 9 },
            new Ability { Id = 41, Code = "DINGHY_DRIVING", Description = "Lái xuồng", AbilitySubgroupId = 9 },
            new Ability { Id = 42, Code = "SPEEDBOAT_DRIVING", Description = "Lái ca nô", AbilitySubgroupId = 9 },
            // SPECIALIZED_DRIVING (subgroup 10)
            new Ability { Id = 43, Code = "NIGHT_VEHICLE_OPERATION", Description = "Điều khiển phương tiện ban đêm", AbilitySubgroupId = 10 },
            new Ability { Id = 44, Code = "RAIN_VEHICLE_OPERATION", Description = "Điều khiển phương tiện trong mưa lớn", AbilitySubgroupId = 10 },
            // TRANSPORT_OPERATIONS (subgroup 11)
            new Ability { Id = 45, Code = "VICTIM_TRANSPORT", Description = "Vận chuyển nạn nhân", AbilitySubgroupId = 11 },
            new Ability { Id = 46, Code = "RELIEF_GOODS_TRANSPORT", Description = "Vận chuyển hàng cứu trợ", AbilitySubgroupId = 11 },
            new Ability { Id = 47, Code = "HEAVY_CARGO_TRANSPORT", Description = "Vận chuyển hàng nặng", AbilitySubgroupId = 11 },
            // FIELD_EXPERIENCE (subgroup 12)
            new Ability { Id = 48, Code = "DISASTER_RELIEF_EXPERIENCE", Description = "Đã tham gia cứu trợ thiên tai", AbilitySubgroupId = 12 },
            new Ability { Id = 49, Code = "FLOOD_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ lũ lụt", AbilitySubgroupId = 12 },
            new Ability { Id = 50, Code = "COMMUNITY_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ cộng đồng", AbilitySubgroupId = 12 },
            // ORGANIZATIONAL_MEMBERSHIP (subgroup 13)
            new Ability { Id = 51, Code = "LOCAL_RESCUE_TEAM_MEMBER", Description = "Thành viên đội cứu hộ địa phương", AbilitySubgroupId = 13 },
            new Ability { Id = 52, Code = "VOLUNTEER_ORG_MEMBER", Description = "Thành viên tổ chức thiện nguyện", AbilitySubgroupId = 13 }
        );
    }

    private static void SeedAssemblyPoints(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var timestampStr = "241015";

        modelBuilder.Entity<AssemblyPoint>().HasData(
            // THỪA THIÊN HUẾ
            new AssemblyPoint
            {
                Id = 1,
                Code = $"AP-HUE-QH-{timestampStr}",
                Name = "Trường THPT Chuyên Quốc Học Huế",
                CapacityTeams = 15,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(107.5925, 16.4608) { SRID = 4326 }, // Hue City
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 2,
                Code = $"AP-HUE-HT-{timestampStr}",
                Name = "UBND Thị xã Hương Trà",
                CapacityTeams = 10,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(107.4566, 16.3986) { SRID = 4326 }, // Huong Tra
                CreatedAt = now,
                UpdatedAt = now
            },
            // QUẢNG BÌNH
            new AssemblyPoint
            {
                Id = 3,
                Code = $"AP-QBI-LT-{timestampStr}",
                Name = "Nhà Văn hóa Huyện Lệ Thủy",
                CapacityTeams = 20,
                Status = AssemblyPointStatus.Overloaded.ToString(),
                Location = new Point(106.7845, 17.2165) { SRID = 4326 }, // Le Thuy (Deep flood)
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 4,
                Code = $"AP-QBI-DH-{timestampStr}",
                Name = "Quảng trường Hồ Chí Minh (Đồng Hới)",
                CapacityTeams = 25,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6186, 17.4706) { SRID = 4326 }, // Dong Hoi
                CreatedAt = now,
                UpdatedAt = now
            },
            // QUẢNG TRỊ
            new AssemblyPoint
            {
                Id = 5,
                Code = $"AP-QTR-DH-{timestampStr}",
                Name = "Nhà Thi đấu Đa năng Quảng Trị",
                CapacityTeams = 15,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(107.1018, 16.8080) { SRID = 4326 }, // Dong Ha
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 6,
                Code = $"AP-QTR-HH-{timestampStr}",
                Name = "Trường Dân tộc Nội trú Hướng Hóa",
                CapacityTeams = 8,
                Status = AssemblyPointStatus.Unavailable.ToString(), // Landslide risk
                Location = new Point(106.7323, 16.6212) { SRID = 4326 }, // Khe Sanh
                CreatedAt = now,
                UpdatedAt = now
            },
            // ĐÀ NẴNG
            new AssemblyPoint
            {
                Id = 7,
                Code = $"AP-DNA-LC-{timestampStr}",
                Name = "Trung tâm Hội chợ Triển lãm Đà Nẵng",
                CapacityTeams = 30,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(108.2205, 16.0500) { SRID = 4326 }, // Cam Le
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 8,
                Code = $"AP-DNA-HV-{timestampStr}",
                Name = "UBND Huyện Hòa Vang",
                CapacityTeams = 12,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(108.1097, 16.0264) { SRID = 4326 }, // Hoa Vang
                CreatedAt = now,
                UpdatedAt = now
            },
            // QUẢNG NAM
            new AssemblyPoint
            {
                Id = 9,
                Code = $"AP-QNA-HA-{timestampStr}",
                Name = "Trường THPT Trần Quý Cáp (Hội An)",
                CapacityTeams = 10,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(108.3380, 15.8801) { SRID = 4326 }, // Hoi An
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 10,
                Code = $"AP-QNA-TM-{timestampStr}",
                Name = "Trung tâm Y tế Nam Trà My",
                CapacityTeams = 5,
                Status = AssemblyPointStatus.Unavailable.ToString(), // Isolated
                Location = new Point(108.0645, 15.1950) { SRID = 4326 }, // Nam Tra My
                CreatedAt = now,
                UpdatedAt = now
            },
            // HÀ TĨNH
            new AssemblyPoint
            {
                Id = 11,
                Code = $"AP-HTI-HK-{timestampStr}",
                Name = "Trường THPT Hương Khê",
                CapacityTeams = 10,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(105.7144, 18.1755) { SRID = 4326 }, // Huong Khe
                CreatedAt = now,
                UpdatedAt = now
            },
            // QUẢNG NGÃI
            new AssemblyPoint
            {
                Id = 12,
                Code = $"AP-QNG-TT-{timestampStr}",
                Name = "UBND Thành phố Quảng Ngãi",
                CapacityTeams = 15,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(108.7987, 15.1207) { SRID = 4326 }, // Quang Ngai City
                CreatedAt = now,
                UpdatedAt = now
            },
            // BÌNH ĐọNH
            new AssemblyPoint
            {
                Id = 13,
                Code = $"AP-BDI-QN-{timestampStr}",
                Name = "Trung tâm Văn hóa Thể thao Quy Nhơn",
                CapacityTeams = 20,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(109.2182, 13.7765) { SRID = 4326 }, // Quy Nhon
                CreatedAt = now,
                UpdatedAt = now
            },
            // PHÚ YÊN
            new AssemblyPoint
            {
                Id = 14,
                Code = $"AP-PYE-TH-{timestampStr}",
                Name = "Sân vận động Tỉnh Phú Yên",
                CapacityTeams = 12,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(109.3241, 13.0955) { SRID = 4326 }, // Tuy Hoa
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedRescueTeams(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeam>().HasData(
            new RescueTeam
            {
                Id = 1,
                Code = "RT-SON-241015120000",
                Name = "Đội Cứu hộ Sông Hương (Huế)",
                TeamType = "Rescue",
                Status = "AwaitingAcceptance",
                AssemblyPointId = 1, // Liên kết tới AP số 1 (Huế)
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 2,
                Code = "RT-PHA-241015120001",
                Name = "Đội Phản ứng nhanh Quảng Bình",
                TeamType = "Medical",
                Status = "Ready",
                AssemblyPointId = 4, // Liên kết tới AP số 4 (Đồng Hới)
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 3,
                Code = "RT-CUU-241015120002",
                Name = "Đội Cứu nạn Vùng cao Nam Trà My",
                TeamType = "Transportation",
                Status = "Gathering",
                AssemblyPointId = 10, // Liên kết tới AP số 10 (Trà My)
                AssemblyDate = now.AddDays(1),
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 4,
                Code = "RT-BIE-241015120003",
                Name = "Biệt đội Ca nô Đà Nẵng",
                TeamType = "Mixed",
                Status = "Available",
                AssemblyPointId = 7, // Liên kết tới AP số 7 (Đà Nẵng)
                AssemblyDate = now.AddHours(2),
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 5,
                Code = "RT-HOI-241015120004",
                Name = "Đội Cứu hộ Cộng đồng Hội An",
                TeamType = "Rescue",
                Status = "Available",
                AssemblyPointId = 9, // Hội An, Quảng Nam
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 6,
                Code = "RT-HVA-241015120005",
                Name = "Đội Cứu nạn Hòa Vang - Đà Nẵng",
                TeamType = "Mixed",
                Status = "Ready",
                AssemblyPointId = 8, // Hòa Vang, Đà Nẵng
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 7,
                Code = "RT-PDI-241015120006",
                Name = "Đội Y tế Khẩn cấp Phong Điền",
                TeamType = "Medical",
                Status = "Available",
                AssemblyPointId = 2, // Hương Trà, Thừa Thiên Huế
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 8,
                Code = "RT-QNG-241015120007",
                Name = "Đội Phản ứng nhanh Quảng Ngãi",
                TeamType = "Rescue",
                Status = "Ready",
                AssemblyPointId = 12, // TT. Quảng Ngãi
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 9,
                Code = "RT-BDI-241015120008",
                Name = "Đội Cứu hộ Ven biển Bình Định",
                TeamType = "Mixed",
                Status = "Available",
                AssemblyPointId = 13, // Quy Nhơn
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 10,
                Code = "RT-HTI-241015120009",
                Name = "Đội Cứu nạn Hương Khê - Hà Tĩnh",
                TeamType = "Transportation",
                Status = "Gathering",
                AssemblyPointId = 11, // Hương Khê, Hà Tĩnh
                AssemblyDate = now.AddDays(2),
                ManagedBy = SeedConstants.CoordinatorUserId,
                MaxMembers = 6,
                CreatedAt = now
            }
        );

        var members = new List<RescueTeamMember>();
        
        // Team 1
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330001"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330002"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330003"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330004"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330005"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = null, RoleInTeam = "Member", Status = "Pending" });
        members.Add(new RescueTeamMember { TeamId = 1, UserId = Guid.Parse("33333333-3333-3333-3333-333333330006"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = null, RoleInTeam = "Member", Status = "Pending" });
        
        // Team 2
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330007"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330008"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330009"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330010"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330011"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 2, UserId = Guid.Parse("33333333-3333-3333-3333-333333330012"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        
        // Team 3
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330013"), CheckedIn = true, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330014"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330015"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330016"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330017"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 3, UserId = Guid.Parse("33333333-3333-3333-3333-333333330018"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        
        // Team 4
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330019"), CheckedIn = true, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330020"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330021"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330022"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330023"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 4, UserId = Guid.Parse("33333333-3333-3333-3333-333333330024"), CheckedIn = true, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 5 - Đội Cứu hộ Hội An
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330025"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330026"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330027"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330028"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330029"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 5, UserId = Guid.Parse("33333333-3333-3333-3333-333333330030"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 6 - Đội Cứu nạn Hòa Vang
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330031"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330032"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330033"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330034"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330035"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 6, UserId = Guid.Parse("33333333-3333-3333-3333-333333330036"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 7 - Đội Y tế Phong Điền
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330037"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330038"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330039"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330040"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330041"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 7, UserId = Guid.Parse("33333333-3333-3333-3333-333333330042"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 8 - Đội Phản ứng nhanh Quảng Ngãi
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330043"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330044"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330045"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330046"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330047"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 8, UserId = Guid.Parse("33333333-3333-3333-3333-333333330048"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 9 - Đội Cứu hộ Bình Định
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330049"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330050"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330051"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330052"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330053"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 9, UserId = Guid.Parse("33333333-3333-3333-3333-333333330054"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        // Team 10 - Đội Cứu nạn Hả Tĩnh
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330055"), CheckedIn = false, InvitedAt = now, IsLeader = true, RespondedAt = now.AddHours(1), RoleInTeam = "Leader", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330056"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330057"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330058"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330059"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });
        members.Add(new RescueTeamMember { TeamId = 10, UserId = Guid.Parse("33333333-3333-3333-3333-333333330060"), CheckedIn = false, InvitedAt = now, IsLeader = false, RespondedAt = now.AddHours(1), RoleInTeam = "Member", Status = "Accepted" });

        modelBuilder.Entity<RescueTeamMember>().HasData(members);
    }

    private static void SeedUserAbilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAbility>().HasData(
            new UserAbility
            {
                UserId = SeedConstants.AdminUserId,
                AbilityId = 1,
                Level = 5
            },
            new UserAbility
            {
                UserId = SeedConstants.CoordinatorUserId,
                AbilityId = 2,
                Level = 4
            }
        );
    }
}
