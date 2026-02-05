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
        SeedAbilities(modelBuilder);
        SeedRescueTeams(modelBuilder);
        SeedAssemblyPoints(modelBuilder);
        SeedUserAbilities(modelBuilder);
    }

    private static void SeedAbilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ability>().HasData(
            new Ability { Id = 1, Code = "BASIC_SWIMMING", Description = "Bơi cơ bản" },
            new Ability { Id = 2, Code = "ADVANCED_SWIMMING", Description = "Bơi thành thạo" },
            new Ability { Id = 3, Code = "WATER_RESCUE", Description = "Cứu hộ dưới nước" },
            new Ability { Id = 4, Code = "DEEP_WATER_MOVEMENT", Description = "Di chuyển trong nước ngập sâu" },
            new Ability { Id = 5, Code = "RAPID_WATER_MOVEMENT", Description = "Di chuyển trong dòng nước chảy xiết" },
            new Ability { Id = 6, Code = "BASIC_DIVING", Description = "Lặn cơ bản" },
            new Ability { Id = 7, Code = "FLOOD_ESCAPE", Description = "Thoát hiểm trong môi trường ngập nước" },
            new Ability { Id = 8, Code = "FLOODED_HOUSE_RESCUE", Description = "Cứu người bị mắc kẹt trong nhà ngập" },
            new Ability { Id = 9, Code = "ROOFTOP_RESCUE", Description = "Cứu người bị mắc kẹt trên mái nhà" },
            new Ability { Id = 10, Code = "VEHICLE_RESCUE", Description = "Cứu người bị kẹt trong phương tiện (xe, ghe)" },
            new Ability { Id = 11, Code = "ROPE_RESCUE", Description = "Sử dụng dây thừng cứu hộ" },
            new Ability { Id = 12, Code = "LIFE_JACKET_USE", Description = "Sử dụng áo phao, phao cứu sinh" },
            new Ability { Id = 13, Code = "NIGHT_RESCUE", Description = "Cứu hộ ban đêm / tầm nhìn kém" },
            new Ability { Id = 14, Code = "STORM_RESCUE", Description = "Cứu hộ trong mưa lớn / bão" },
            new Ability { Id = 15, Code = "DEBRIS_RESCUE", Description = "Cứu hộ tại khu vực đổ nát" },
            new Ability { Id = 16, Code = "HAZARDOUS_RESCUE", Description = "Cứu hộ trong môi trường nguy hiểm" },
            new Ability { Id = 17, Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản" },
            new Ability { Id = 18, Code = "OPEN_WOUND_CARE", Description = "Sơ cứu vết thương hở" },
            new Ability { Id = 19, Code = "BLEEDING_CONTROL", Description = "Cầm máu" },
            new Ability { Id = 20, Code = "WOUND_BANDAGING", Description = "Băng bó vết thương" },
            new Ability { Id = 21, Code = "MINOR_INJURY_CARE", Description = "Xử lý trầy xước, chấn thương nhẹ" },
            new Ability { Id = 22, Code = "MINOR_BURN_CARE", Description = "Xử lý bỏng nhẹ" },
            new Ability { Id = 23, Code = "CPR", Description = "Hồi sức tim phổi (CPR)" },
            new Ability { Id = 24, Code = "DROWNING_RESPONSE", Description = "Xử lý đuối nước" },
            new Ability { Id = 25, Code = "SHOCK_TREATMENT", Description = "Xử lý sốc" },
            new Ability { Id = 26, Code = "HYPOTHERMIA_TREATMENT", Description = "Xử lý hạ thân nhiệt" },
            new Ability { Id = 27, Code = "VITAL_SIGNS_MONITORING", Description = "Theo dõi dấu hiệu sinh tồn" },
            new Ability { Id = 28, Code = "VICTIM_ASSESSMENT", Description = "Đánh giá mức độ nguy kịch nạn nhân" },
            new Ability { Id = 29, Code = "FRACTURE_IMMOBILIZATION", Description = "Cố định gãy xương tạm thời" },
            new Ability { Id = 30, Code = "SPINAL_INJURY_CARE", Description = "Xử lý chấn thương cột sống (cơ bản)" },
            new Ability { Id = 31, Code = "SAFE_PATIENT_TRANSPORT", Description = "Vận chuyển người bị thương an toàn" },
            new Ability { Id = 32, Code = "MEDICAL_STAFF", Description = "Nhân viên y tế" },
            new Ability { Id = 33, Code = "NURSE", Description = "Y tá" },
            new Ability { Id = 34, Code = "DOCTOR", Description = "Bác sĩ" },
            new Ability { Id = 35, Code = "PREHOSPITAL_EMERGENCY", Description = "Cấp cứu tiền viện" },
            new Ability { Id = 36, Code = "MOTORCYCLE_DRIVING", Description = "Lái xe máy" },
            new Ability { Id = 37, Code = "MOTORCYCLE_FLOOD_DRIVING", Description = "Lái xe máy trong điều kiện ngập nước" },
            new Ability { Id = 38, Code = "CAR_DRIVING", Description = "Lái ô tô" },
            new Ability { Id = 39, Code = "OFFROAD_DRIVING", Description = "Lái ô tô địa hình" },
            new Ability { Id = 40, Code = "ROWBOAT_DRIVING", Description = "Lái ghe" },
            new Ability { Id = 41, Code = "DINGHY_DRIVING", Description = "Lái xuồng" },
            new Ability { Id = 42, Code = "SPEEDBOAT_DRIVING", Description = "Lái ca nô" },
            new Ability { Id = 43, Code = "NIGHT_VEHICLE_OPERATION", Description = "Điều khiển phương tiện ban đêm" },
            new Ability { Id = 44, Code = "RAIN_VEHICLE_OPERATION", Description = "Điều khiển phương tiện trong mưa lớn" },
            new Ability { Id = 45, Code = "VICTIM_TRANSPORT", Description = "Vận chuyển nạn nhân" },
            new Ability { Id = 46, Code = "RELIEF_GOODS_TRANSPORT", Description = "Vận chuyển hàng cứu trợ" },
            new Ability { Id = 47, Code = "HEAVY_CARGO_TRANSPORT", Description = "Vận chuyển hàng nặng" },
            new Ability { Id = 48, Code = "DISASTER_RELIEF_EXPERIENCE", Description = "Đã tham gia cứu trợ thiên tai" },
            new Ability { Id = 49, Code = "FLOOD_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ lũ lụt" },
            new Ability { Id = 50, Code = "COMMUNITY_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ cộng đồng" },
            new Ability { Id = 51, Code = "RESCUE_CERTIFICATE", Description = "Chứng chỉ cứu hộ" },
            new Ability { Id = 52, Code = "FIRST_AID_CERTIFICATE", Description = "Chứng chỉ sơ cứu / y tế" },
            new Ability { Id = 53, Code = "LOCAL_RESCUE_TEAM_MEMBER", Description = "Thành viên đội cứu hộ địa phương" },
            new Ability { Id = 54, Code = "VOLUNTEER_ORG_MEMBER", Description = "Thành viên tổ chức thiện nguyện" }
        );
    }

    private static void SeedRescueTeams(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeam>().HasData(
            new RescueTeam
            {
                Id = 1,
                Name = "Đội Cứu hộ Sông Hương (Huế)",
                Location = new Point(107.5925, 16.4608) { SRID = 4326 }, // Hue City
                Status = "Available",
                MaxMembers = 15,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 2,
                Name = "Đội Phản ứng nhanh Quảng Bình",
                Location = new Point(106.6186, 17.4706) { SRID = 4326 }, // Dong Hoi
                Status = "OnMission",
                MaxMembers = 12,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 3,
                Name = "Đội Cứu nạn Vùng cao Nam Trà My",
                Location = new Point(108.0645, 15.1950) { SRID = 4326 }, // Nam Tra My
                Status = "Available",
                MaxMembers = 10,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 4,
                Name = "Biệt đội Ca nô Đà Nẵng",
                Location = new Point(108.2205, 16.0500) { SRID = 4326 }, // Da Nang
                Status = "Available",
                MaxMembers = 20,
                CreatedAt = now
            }
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
            }
        );
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
