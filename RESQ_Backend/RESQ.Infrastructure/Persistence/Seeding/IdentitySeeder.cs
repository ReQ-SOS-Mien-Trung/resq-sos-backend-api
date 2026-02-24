using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class IdentitySeeder
{
    public static void SeedIdentity(this ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedUsers(modelBuilder);
        SeedRescuerApplications(modelBuilder);
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

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = SeedConstants.AdminUserId,
                RoleId = 1,
                FullName = "Nguyễn Văn Admin",
                Username = "admin",
                Phone = "0901234567",
                Password = SeedConstants.AdminPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.CoordinatorUserId,
                RoleId = 2,
                FullName = "Trần Thị Coordinator",
                Username = "coordinator",
                Phone = "0912345678",
                Password = SeedConstants.CoordinatorPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.RescuerUserId,
                RoleId = 3,
                FullName = "Lê Văn Rescuer",
                Username = "rescuer",
                Phone = "0923456789",
                Password = SeedConstants.RescuerPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.ManagerUserId,
                RoleId = 4,
                FullName = "Phạm Thị Manager",
                Username = "manager",
                Phone = "0934567890",
                Password = SeedConstants.ManagerPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.VictimUserId,
                RoleId = 5,
                FullName = "Hoàng Văn Victim",
                Username = "victim",
                Phone = "0945678901",
                Password = SeedConstants.VictimPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // Applicant users - người nộp đơn đăng ký rescuer
            new User
            {
                Id = SeedConstants.Applicant1UserId,
                RoleId = 5, // Victim role - chưa được duyệt thành Rescuer
                FullName = "Nguyễn Thanh Tùng",
                Username = "applicant1",
                Phone = "0961111111",
                Email = "tung.nguyen@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = "Volunteer",
                Address = "123 Nguyễn Huệ, Quận 1",
                Ward = "Bến Nghé",
                City = "TP. Hồ Chí Minh",
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant2UserId,
                RoleId = 5,
                FullName = "Trần Minh Đức",
                Username = "applicant2",
                Phone = "0962222222",
                Email = "duc.tran@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = "Professional",
                Address = "456 Lê Lợi, Quận 3",
                Ward = "Phường 7",
                City = "TP. Hồ Chí Minh",
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant3UserId,
                RoleId = 5,
                FullName = "Lê Thị Hương",
                Username = "applicant3",
                Phone = "0963333333",
                Email = "huong.le@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = "Volunteer",
                Address = "789 Trần Hưng Đạo, Quận 5",
                Ward = "Phường 11",
                City = "TP. Hồ Chí Minh",
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.Applicant4UserId,
                RoleId = 3, // Đã được duyệt thành Rescuer
                FullName = "Phạm Văn Hải",
                Username = "applicant4",
                Phone = "0964444444",
                Email = "hai.pham@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = "Professional",
                Address = "321 Hai Bà Trưng, Quận 1",
                Ward = "Đa Kao",
                City = "TP. Hồ Chí Minh",
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
                FullName = "Võ Thị Mai",
                Username = "applicant5",
                Phone = "0965555555",
                Email = "mai.vo@email.com",
                Password = SeedConstants.ApplicantPasswordHash,
                RescuerType = "Volunteer",
                Address = "654 Cách Mạng Tháng Tám, Quận 10",
                Ward = "Phường 13",
                City = "TP. Hồ Chí Minh",
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
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
                Status = "Pending",
                SubmittedAt = now.AddDays(1)
            },
            // 2. Đơn Pending - chờ duyệt
            new RescuerApplication
            {
                Id = 2,
                UserId = SeedConstants.Applicant2UserId,
                Status = "Pending",
                SubmittedAt = now.AddDays(2)
            },
            // 3. Đơn Pending - chờ duyệt
            new RescuerApplication
            {
                Id = 3,
                UserId = SeedConstants.Applicant3UserId,
                Status = "Pending",
                SubmittedAt = now.AddDays(3)
            },
            // 4. Đơn Approved - đã duyệt
            new RescuerApplication
            {
                Id = 4,
                UserId = SeedConstants.Applicant4UserId,
                Status = "Approved",
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
                Status = "Rejected",
                SubmittedAt = now.AddDays(2),
                ReviewedAt = now.AddDays(6),
                ReviewedBy = SeedConstants.AdminUserId,
                AdminNote = "Chưa đủ 18 tuổi. Vui lòng nộp lại khi đủ điều kiện."
            }
        );

        // Seed documents cho các đơn đăng ký
        modelBuilder.Entity<RescuerApplicationDocument>().HasData(
            // Documents cho Applicant 1 (Pending)
            new RescuerApplicationDocument
            {
                Id = 1,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_cccd_front.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 2,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_cccd_back.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 3,
                ApplicationId = 1,
                FileUrl = "https://storage.example.com/documents/applicant1_health_cert.pdf",
                FileType = "HealthCertificate",
                UploadedAt = now.AddDays(1)
            },
            // Documents cho Applicant 2 (Pending)
            new RescuerApplicationDocument
            {
                Id = 4,
                ApplicationId = 2,
                FileUrl = "https://storage.example.com/documents/applicant2_cccd_front.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(2)
            },
            new RescuerApplicationDocument
            {
                Id = 5,
                ApplicationId = 2,
                FileUrl = "https://storage.example.com/documents/applicant2_rescue_cert.pdf",
                FileType = "RescueCertificate",
                UploadedAt = now.AddDays(2)
            },
            // Documents cho Applicant 3 (Pending)
            new RescuerApplicationDocument
            {
                Id = 6,
                ApplicationId = 3,
                FileUrl = "https://storage.example.com/documents/applicant3_cccd_front.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(3)
            },
            // Documents cho Applicant 4 (Approved)
            new RescuerApplicationDocument
            {
                Id = 7,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_cccd_front.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 8,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_first_aid_cert.pdf",
                FileType = "FirstAidCertificate",
                UploadedAt = now.AddDays(1)
            },
            new RescuerApplicationDocument
            {
                Id = 9,
                ApplicationId = 4,
                FileUrl = "https://storage.example.com/documents/applicant4_experience_letter.pdf",
                FileType = "ExperienceLetter",
                UploadedAt = now.AddDays(1)
            },
            // Documents cho Applicant 5 (Rejected)
            new RescuerApplicationDocument
            {
                Id = 10,
                ApplicationId = 5,
                FileUrl = "https://storage.example.com/documents/applicant5_cccd_front.jpg",
                FileType = "CCCD",
                UploadedAt = now.AddDays(2)
            }
        );
    }
}
