using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Personnel; // Added namespace for AssemblyPointStatus
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
        // (Abilities data retained as is...)
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
        var now = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now2 = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescueTeam>().HasData(
            new RescueTeam
            {
                Id = 1,
                Name = "Đội cứu hộ Alpha",
                Location = new Point(106.7009, 10.7769) { SRID = 4326 },
                Status = "Available", // Keeping string as RescueTeamStatus enum file is not available in context
                MaxMembers = 10,
                CreatedAt = now
            },
            new RescueTeam
            {
                Id = 2,
                Name = "Đội cứu hộ Beta",
                Location = new Point(106.7218, 10.7380) { SRID = 4326 },
                Status = "OnMission", // Keeping string as RescueTeamStatus enum file is not available in context
                MaxMembers = 8,
                CreatedAt = now2
            }
        );
    }

    private static void SeedAssemblyPoints(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 2, 4, 12, 0, 0, DateTimeKind.Utc);
        var timestampStr = "240204120000";

        modelBuilder.Entity<AssemblyPoint>().HasData(
            new AssemblyPoint
            {
                Id = 1,
                Code = $"AP-CEN-{timestampStr}",
                Name = "Trung tâm Văn hóa Quận 1",
                CapacityTeams = 10,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6961, 10.7925) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 2,
                Code = $"AP-HOA-{timestampStr}",
                Name = "Sân vận động Hoa Lư",
                CapacityTeams = 20,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.7020, 10.7890) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 3,
                Code = $"AP-LEQ-{timestampStr}",
                Name = "Trường THPT Lê Quý Đôn",
                CapacityTeams = 8,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6930, 10.7780) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 4,
                Code = $"AP-CON-{timestampStr}",
                Name = "Công viên 23/9",
                CapacityTeams = 15,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6940, 10.7680) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 5,
                Code = $"AP-PHU-{timestampStr}",
                Name = "Nhà thi đấu Phú Thọ",
                CapacityTeams = 25,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6560, 10.7685) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 6,
                Code = $"AP-BAC-{timestampStr}",
                Name = "Đại học Bách Khoa TP.HCM",
                CapacityTeams = 20,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6578, 10.7725) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 7,
                Code = $"AP-UBN-{timestampStr}",
                Name = "UBND Quận 4",
                CapacityTeams = 6,
                Status = AssemblyPointStatus.Overloaded.ToString(),
                Location = new Point(106.7050, 10.7600) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 8,
                Code = $"AP-CAN-{timestampStr}",
                Name = "Cảng Nhà Rồng",
                CapacityTeams = 12,
                Status = AssemblyPointStatus.Unavailable.ToString(),
                Location = new Point(106.7070, 10.7680) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 9,
                Code = $"AP-NGU-{timestampStr}",
                Name = "Trường Tiểu học Nguyễn Thái Học",
                CapacityTeams = 5,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.6965, 10.7630) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 10,
                Code = $"AP-THE-{timestampStr}",
                Name = "Trung tâm Thể thao Quận 7",
                CapacityTeams = 18,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.7350, 10.7420) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 11,
                Code = $"AP-CRE-{timestampStr}",
                Name = "Crescent Mall",
                CapacityTeams = 10,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.7210, 10.7290) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 12,
                Code = $"AP-CAU-{timestampStr}",
                Name = "Công viên Cầu Ánh Sao",
                CapacityTeams = 8,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.7230, 10.7280) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 13,
                Code = $"AP-MIE-{timestampStr}",
                Name = "Bến xe Miền Đông (Cũ)",
                CapacityTeams = 15,
                Status = AssemblyPointStatus.Unavailable.ToString(),
                Location = new Point(106.7120, 10.8130) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 14,
                Code = $"AP-VAN-{timestampStr}",
                Name = "Khu du lịch Văn Thánh",
                CapacityTeams = 12,
                Status = AssemblyPointStatus.Active.ToString(),
                Location = new Point(106.7190, 10.7960) { SRID = 4326 },
                CreatedAt = now,
                UpdatedAt = now
            },
            new AssemblyPoint
            {
                Id = 15,
                Code = $"AP-VIN-{timestampStr}",
                Name = "Vinhomes Central Park",
                CapacityTeams = 20,
                Status = AssemblyPointStatus.Overloaded.ToString(),
                Location = new Point(106.7220, 10.7950) { SRID = 4326 },
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
