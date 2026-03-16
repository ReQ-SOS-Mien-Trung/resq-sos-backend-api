using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class IdentitySeeder
{
    public static void SeedIdentity(this ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedDocumentFileTypeCategories(modelBuilder);
        SeedUsers(modelBuilder);
        SeedRescuerApplications(modelBuilder);
    }

    private static void SeedDocumentFileTypeCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentFileTypeCategory>().HasData(
            new DocumentFileTypeCategory { Id = 1, Code = "RESCUE", Description = "Tài liệu danh mục cứu hộ" },
            new DocumentFileTypeCategory { Id = 2, Code = "MEDICAL", Description = "Tài liệu danh mục y tế" },
            new DocumentFileTypeCategory { Id = 3, Code = "TRANSPORTATION", Description = "Tài liệu danh mục vận chuyển" },
            new DocumentFileTypeCategory { Id = 4, Code = "OTHER", Description = "Tài liệu danh mục khác" }
        );
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

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var users = new List<User>
        {
            new User
            {
                Id = SeedConstants.AdminUserId,
                RoleId = 1,
                FirstName = "Tuấn",
                LastName = "Nguyễn Minh",
                Username = "admin",
                Phone = "0901234567",
                Password = SeedConstants.AdminPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.CoordinatorUserId,
                RoleId = 2,
                FirstName = "Nhung",
                LastName = "Trần Thị Hồng",
                Username = "coordinator",
                Phone = "0912345678",
                Password = SeedConstants.CoordinatorPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.RescuerUserId,
                RoleId = 3,
                FirstName = "Hải",
                LastName = "Lê Quang",
                Username = "rescuer",
                Phone = "0923456789",
                Password = SeedConstants.RescuerPasswordHash,
                RescuerType = RescuerType.Core.ToString(),
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.ManagerUserId,
                RoleId = 4,
                FirstName = "Lan",
                LastName = "Phạm Thị Mai",
                Username = "manager",
                Phone = "0934567890",
                Password = SeedConstants.ManagerPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Manager2UserId,
                RoleId = 4,
                FirstName = "Khoa",
                LastName = "Ngô Đình",
                Username = "manager2",
                Phone = "0934567891",
                Password = SeedConstants.ManagerPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Manager3UserId,
                RoleId = 4,
                FirstName = "Đức",
                LastName = "Hoàng Văn",
                Username = "manager3",
                Phone = "0934567892",
                Password = SeedConstants.ManagerPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Manager4UserId,
                RoleId = 4,
                FirstName = "Thảo",
                LastName = "Bùi Thị Thanh",
                Username = "manager4",
                Phone = "0934567893",
                Password = SeedConstants.ManagerPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.VictimUserId,
                RoleId = 5,
                FirstName = "Tâm",
                LastName = "Võ Văn",
                Username = "victim",
                Phone = "0945678901",
                Password = SeedConstants.VictimPasswordHash,
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // Applicant users - người nộp đơn đăng ký rescuer
            new User
            {
                Id = SeedConstants.Applicant1UserId,
                RoleId = 5, // Victim role - chưa được duyệt thành Rescuer
                FirstName = "Tùng",
                LastName = "Nguyễn Thanh",
                Username = "applicant1",
                Phone = "0961111111",
                Email = "tung.nguyen@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = RescuerType.Volunteer.ToString(),
                Address = "15 Lê Lợi, TP. Huế",
                Ward = "Phú Hội",
                Province = "Thừa Thiên Huế",
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant2UserId,
                RoleId = 5,
                FirstName = "Đức",
                LastName = "Trần Minh",
                Username = "applicant2",
                Phone = "0962222222",
                Email = "duc.tran@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = RescuerType.Core.ToString(),
                Address = "120 Trưng Nữ Vương, Hải Châu",
                Ward = "Hải Châu 1",
                Province = "Đà Nẵng",
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant3UserId,
                RoleId = 5,
                FirstName = "Hương",
                LastName = "Lê Thị",
                Username = "applicant3",
                Phone = "0963333333",
                Email = "huong.le@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = RescuerType.Volunteer.ToString(),
                Address = "22 Hùng Vương, Đông Hà",
                Ward = "Phường 1",
                Province = "Quảng Trị",
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant4UserId,
                RoleId = 3, // Đã được duyệt thành Rescuer
                FirstName = "Hải",
                LastName = "Phạm Văn",
                Username = "applicant4",
                Phone = "0964444444",
                Email = "hai.pham@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = RescuerType.Core.ToString(),
                Address = "45 Quang Trung, Đồng Hới",
                Ward = "Hải Đình",
                Province = "Quảng Bình",
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                ApprovedBy = SeedConstants.AdminUserId,
                ApprovedAt = now.AddDays(5),
                CreatedAt = now,
                UpdatedAt = now.AddDays(5)
            },
            new User
            {
                Id = SeedConstants.Applicant5UserId,
                RoleId = 5,
                FirstName = "Mai",
                LastName = "Võ Thị",
                Username = "applicant5",
                Phone = "0965555555",
                Email = "mai.vo@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = RescuerType.Volunteer.ToString(),
                Address = "88 Phan Đình Phùng, TP. Hà Tĩnh",
                Ward = "Bắc Hà",
                Province = "Hà Tĩnh",
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        // Thêm 80 Rescuers (60 trong team, 20 free cho việc test tạo Team)
        var ho = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
        var dem = new[] { "Văn", "Thị", "Đức", "Ngọc", "Minh", "Phương", "Quang", "Thanh", "Hữu", "Hồng" };
        var ten = new[] { "An", "Bình", "Cường", "Dũng", "Đạt", "Giang", "Hà", "Hải", "Hạnh", "Hiếu", "Hoà", "Hùng", "Hương", "Khánh", "Khoa", "Lâm", "Linh", "Long", "Mai", "Minh", "Nam", "Ngọc", "Nhân", "Phong", "Phúc", "Quân", "Sơn", "Tâm", "Thắng", "Thiện", "Thịnh", "Thuỷ", "Toàn", "Trung", "Tuấn", "Tùng", "Uyên", "Vinh", "Vũ", "Yến" };

        for (int i = 1; i <= 80; i++)
        {
            var idStr = i.ToString("D4");
            users.Add(new User
            {
                Id = Guid.Parse($"33333333-3333-3333-3333-33333333{idStr}"),
                RoleId = 3,
                FirstName = ten[(i - 1) % ten.Length],
                LastName = $"{ho[(i - 1) % ho.Length]} {dem[(i - 1) % dem.Length]}",
                Username = $"rescuer{i}",
                Phone = $"09{i:D8}",
                Email = $"rescuer{i}@fpt.edu.vn",
                Password = SeedConstants.RescuerPasswordHash,
                RescuerType = (i % 2 == 0) ? RescuerType.Core.ToString() : RescuerType.Volunteer.ToString(),
                IsEmailVerified = true,
                IsOnboarded = true,
                IsEligibleRescuer = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        modelBuilder.Entity<User>().HasData(users);
    }

    private static void SeedRescuerApplications(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed Rescuer Applications với nhiều trạng thái khác nhau
        modelBuilder.Entity<RescuerApplication>().HasData(
            // 1. Đơn Pending - chờ duyệt
            new RescuerApplication
            {
                Id = 1,
                UserId = SeedConstants.Applicant1UserId,
                Status = RescuerApplicationStatus.Pending.ToString(),
                SubmittedAt = now.AddDays(1)
            },
            // 2. Đơn Pending - chờ duyệt
            new RescuerApplication
            {
                Id = 2,
                UserId = SeedConstants.Applicant2UserId,
                Status = RescuerApplicationStatus.Pending.ToString(),
                SubmittedAt = now.AddDays(2)
            },
            // 3. Đơn Pending - chờ duyệt
            new RescuerApplication
            {
                Id = 3,
                UserId = SeedConstants.Applicant3UserId,
                Status = RescuerApplicationStatus.Pending.ToString(),
                SubmittedAt = now.AddDays(3)
            },
            // 4. Đơn Approved - đã duyệt
            new RescuerApplication
            {
                Id = 4,
                UserId = SeedConstants.Applicant4UserId,
                Status = RescuerApplicationStatus.Approved.ToString(),
                SubmittedAt = now.AddDays(1),
                ReviewedAt = now.AddDays(5),
                ReviewedBy = SeedConstants.AdminUserId,
                AdminNote = "Đủ điều kiện. Có chứng chỉ sơ cấp cứu và kinh nghiệm cứu hộ."
            },
            // 5. Đơn Rejected - đã từ chối
            new RescuerApplication
            {
                Id = 5,
                UserId = SeedConstants.Applicant5UserId,
                Status = RescuerApplicationStatus.Rejected.ToString(),
                SubmittedAt = now.AddDays(2),
                ReviewedAt = now.AddDays(6),
                ReviewedBy = SeedConstants.AdminUserId,
                AdminNote = "Chưa đủ 18 tuổi. Vui lòng nộp lại khi đủ điều kiện."
            }
        );

        // Seed document file types từ enum
        modelBuilder.Entity<DocumentFileType>().HasData(
            // RESCUE
            new DocumentFileType { Id = 1, Code = "WATER_SAFETY_CERT", Name = "Chứng chỉ an toàn dưới nước", Description = "Chứng chỉ xác nhận khả năng bơi lội, sinh tồn và an toàn môi trường nước cơ bản.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 2, Code = "WATER_RESCUE_CERT", Name = "Chứng chỉ cứu hộ dưới nước", Description = "Chứng chỉ nghiệp vụ cứu hộ, cứu nạn chuyên nghiệp dưới nước, dòng chảy xiết.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 3, Code = "TECHNICAL_RESCUE_CERT", Name = "Chứng chỉ cứu hộ kỹ thuật", Description = "Chứng chỉ nghiệp vụ sử dụng thiết bị chuyên dụng, cứu hộ không gian hẹp, sập đổ, dùng dây thừng.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 4, Code = "DISASTER_RESPONSE_CERT", Name = "Chứng chỉ ứng phó thiên tai", Description = "Chứng chỉ hoàn thành khóa huấn luyện phản ứng nhanh, điều phối và ứng phó thảm họa/thiên tai.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            // MEDICAL
            new DocumentFileType { Id = 5, Code = "BASIC_FIRST_AID_CERT", Name = "Chứng chỉ Sơ cấp cứu cơ bản", Description = "Chứng chỉ hoàn thành các khóa đào tạo sơ cấp cứu ban đầu, hô hấp nhân tạo, dành cho tình nguyện viên và nhân viên y tế nền tảng.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 6, Code = "NURSING_PRACTICE_LICENSE", Name = "Chứng chỉ hành nghề Điều dưỡng", Description = "Giấy phép hành nghề điều dưỡng, y tá do cơ quan có thẩm quyền cấp, chứng minh năng lực thực hành lâm sàng và chăm sóc người bệnh.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 10, Code = "PARAMEDIC_EMT_CERT", Name = "Chứng chỉ Cấp cứu ngoại viện", Description = "Chứng chỉ chuyên môn dành cho lực lượng cấp cứu tiền viện (115/EMT), chuyên gia xử lý chấn thương và duy trì sự sống trực tiếp tại hiện trường.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 11, Code = "MEDICAL_DOCTOR_LICENSE", Name = "Chứng chỉ hành nghề Bác sĩ", Description = "Giấy phép hành nghề khám, chữa bệnh cấp cho Bác sĩ. Thể hiện thẩm quyền cao nhất trong chẩn đoán, phân loại mức độ nguy kịch và ra y lệnh.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            // TRANSPORTATION
            new DocumentFileType { Id = 7, Code = "MOTORCYCLE_LICENSE", Name = "Giấy phép lái xe máy", Description = "Bằng lái xe mô tô 2 bánh (Hạng A1, A2...).", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 8, Code = "CAR_TRUCK_LICENSE", Name = "Giấy phép lái xe ô tô / tải", Description = "Bằng lái xe ô tô, xe bán tải, xe tải hạng nặng (Hạng B1, B2, C, D...).", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 12, Code = "INLAND_WATERWAY_LICENSE", Name = "Bằng lái phương tiện thủy", Description = "Chứng chỉ/Bằng lái phương tiện thủy nội địa dành cho người điều khiển Ca nô, xuồng máy có động cơ.", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now },
            // OTHER
            new DocumentFileType { Id = 9, Code = "OTHER", Name = "Khác", Description = "Khác", IsActive = true, DocumentFileTypeCategoryId = 4, CreatedAt = now, UpdatedAt = now }
        );

        // Seed documents cho các đơn đăng ký
        modelBuilder.Entity<RescuerApplicationDocument>().HasData(
            // Documents cho Applicant 1 (Pending)
            new RescuerApplicationDocument
            {
                Id = 1,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_cccd_front.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 2,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_cccd_back.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 3,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_health_cert.pdf",
                FileTypeId = 5, // BASIC_MEDICAL_CERT
                UploadedAt = now.AddDays(1)
            },
            // Documents cho Applicant 2 (Pending)
            new RescuerApplicationDocument
            {
                Id = 4,
                ApplicationId = 2,
                FileUrl = "https://storage.example.com/documents/applicant2_cccd_front.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(2)
            },
            new RescuerApplicationDocument
            {
                Id = 5,
                ApplicationId = 2,
                FileUrl = "https://storage.example.com/documents/applicant2_rescue_cert.pdf",
                FileTypeId = 2, // WATER_RESCUE_CERT
                UploadedAt = now.AddDays(2)
            },
            // Documents cho Applicant 3 (Pending)
            new RescuerApplicationDocument
            {
                Id = 6,
                ApplicationId = 3,
                FileUrl = "https://storage.example.com/documents/applicant3_cccd_front.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(3)
            },
            // Documents cho Applicant 4 (Approved)
            new RescuerApplicationDocument
            {
                Id = 7,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_cccd_front.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 8,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_first_aid_cert.pdf",
                FileTypeId = 5, // BASIC_MEDICAL_CERT
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 9,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_experience_letter.pdf",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(1)
            },
            // Documents cho Applicant 5 (Rejected)
            new RescuerApplicationDocument
            {
                Id = 10,
                ApplicationId = 5,
                FileUrl = "https://storage.example.com/documents/applicant5_cccd_front.jpg",
                FileTypeId = 9, // OTHER
                UploadedAt = now.AddDays(2)
            }
        );
    }
}
