using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class StaticModelSeeder
{
    public static void SeedStaticModelData(this ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedDocumentFileTypes(modelBuilder);
        modelBuilder.SeedPermission();
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Coordinator" },
            new Role { Id = 3, Name = "Rescuer" },
            new Role { Id = 4, Name = "Manager" },
            new Role { Id = 5, Name = "Victim" });
    }

    private static void SeedDocumentFileTypes(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DocumentFileTypeCategory>().HasData(
            new DocumentFileTypeCategory { Id = 1, Code = "RESCUE", Description = "Tài liệu danh mục cứu hộ" },
            new DocumentFileTypeCategory { Id = 2, Code = "MEDICAL", Description = "Tài liệu danh mục y tế" },
            new DocumentFileTypeCategory { Id = 3, Code = "TRANSPORTATION", Description = "Tài liệu danh mục vận chuyển" },
            new DocumentFileTypeCategory { Id = 4, Code = "OTHER", Description = "Tài liệu danh mục khác" });

        modelBuilder.Entity<DocumentFileType>().HasData(
            new DocumentFileType { Id = 1, Code = "WATER_SAFETY_CERT", Name = "Chứng chỉ an toàn dưới nước", Description = "Khả năng bơi lội, sinh tồn và an toàn môi trường nước.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 2, Code = "WATER_RESCUE_CERT", Name = "Chứng chỉ cứu hộ dưới nước", Description = "Nghiệp vụ cứu hộ dưới nước, dòng chảy xiết.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 3, Code = "TECHNICAL_RESCUE_CERT", Name = "Chứng chỉ cứu hộ kỹ thuật", Description = "Sử dụng thiết bị chuyên dụng, cứu hộ không gian hẹp, sập đổ.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 4, Code = "DISASTER_RESPONSE_CERT", Name = "Chứng chỉ ứng phó thiên tai", Description = "Huấn luyện phản ứng nhanh và điều phối thiên tai.", IsActive = true, DocumentFileTypeCategoryId = 1, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 5, Code = "BASIC_FIRST_AID_CERT", Name = "Chứng chỉ sơ cấp cứu cơ bản", Description = "Sơ cứu ban đầu, hô hấp nhân tạo và xử lý chấn thương nhẹ.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 6, Code = "NURSING_PRACTICE_LICENSE", Name = "Chứng chỉ hành nghề điều dưỡng", Description = "Giấy phép hành nghề điều dưỡng/y tá.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 7, Code = "MOTORCYCLE_LICENSE", Name = "Giấy phép lái xe máy", Description = "Bằng lái xe mô tô 2 bánh.", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 8, Code = "CAR_TRUCK_LICENSE", Name = "Giấy phép lái xe ô tô/tải", Description = "Bằng lái ô tô, bán tải hoặc xe tải.", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 9, Code = "OTHER", Name = "Khác", Description = "Tài liệu bổ sung khác.", IsActive = true, DocumentFileTypeCategoryId = 4, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 10, Code = "PARAMEDIC_EMT_CERT", Name = "Chứng chỉ cấp cứu ngoại viện", Description = "Cấp cứu tiền viện và duy trì sự sống tại hiện trường.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 11, Code = "MEDICAL_DOCTOR_LICENSE", Name = "Chứng chỉ hành nghề bác sĩ", Description = "Giấy phép hành nghề khám, chữa bệnh.", IsActive = true, DocumentFileTypeCategoryId = 2, CreatedAt = now, UpdatedAt = now },
            new DocumentFileType { Id = 12, Code = "INLAND_WATERWAY_LICENSE", Name = "Bằng lái phương tiện thủy", Description = "Điều khiển ca nô, xuồng máy hoặc phương tiện thủy nội địa.", IsActive = true, DocumentFileTypeCategoryId = 3, CreatedAt = now, UpdatedAt = now });
    }
}
