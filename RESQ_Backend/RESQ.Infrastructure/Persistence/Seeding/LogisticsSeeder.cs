using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class LogisticsSeeder
{
    public static void SeedLogistics(this ModelBuilder modelBuilder)
    {
        SeedCategories(modelBuilder);
        SeedOrganizations(modelBuilder);
        SeedReliefItems(modelBuilder);
        SeedDepots(modelBuilder);
        SeedDepotManagers(modelBuilder);
        SeedDepotInventories(modelBuilder);
        SeedInventoryLogs(modelBuilder);
        SeedOrganizationReliefItems(modelBuilder);
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
                Description = "Nhu yếu phẩm, gạo, mì, nước uống",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ItemCategory
            {
                Id = 2,
                Code = "MEDICAL",
                Name = "Y tế & Cứu hộ",
                Description = "Thuốc men, dụng cụ sơ cứu, áo phao",
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
                Name = "Hội Chữ Thập Đỏ - Chi nhánh Miền Trung",
                Phone = "02343822123",
                Email = "central@redcross.org.vn",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Organization
            {
                Id = 2,
                Name = "Quỹ Hỗ trợ Thiên tai ABC",
                Phone = "02363567890",
                Email = "contact@abc-relief.org",
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
            // ID 1: General Food Item
            new ReliefItem
            {
                Id = 1,
                CategoryId = 1,
                Name = "Gạo & Lương khô (Combo)",
                Unit = "phần",
                TargetGroup = "Tất cả",
                CreatedAt = now,
                UpdatedAt = now
            },
            // ID 2: General Rescue/Medical Item
            new ReliefItem
            {
                Id = 2,
                CategoryId = 2,
                Name = "Bộ cứu thương & Áo phao",
                Unit = "bộ",
                TargetGroup = "Cứu hộ",
                CreatedAt = now,
                UpdatedAt = now
            },
            // New Items (Optional, referenced in Inventory but not Missions to avoid FK errors)
            new ReliefItem
            {
                Id = 3,
                CategoryId = 1,
                Name = "Mì tôm",
                Unit = "thùng",
                TargetGroup = "Tất cả",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ReliefItem
            {
                Id = 4,
                CategoryId = 1,
                Name = "Nước sạch",
                Unit = "thùng",
                TargetGroup = "Tất cả",
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Depot>().HasData(
            new Depot
            {
                Id = 1,
                Name = "Kho Cứu trợ Trung tâm Huế",
                Address = "15 Lê Lợi, TP. Huế",
                Location = new Point(107.5950, 16.4650) { SRID = 4326 },
                Status = DepotStatus.Available.ToString(),
                Capacity = 5000,
                CurrentUtilization = 3000,
                LastUpdatedAt = now
            },
            new Depot
            {
                Id = 2,
                Name = "Kho Tiền phương Lệ Thủy",
                Address = "TT. Kiến Giang, Lệ Thủy, QB",
                Location = new Point(106.7820, 17.2150) { SRID = 4326 },
                Status = DepotStatus.Full.ToString(), 
                Capacity = 2000,
                CurrentUtilization = 1800,
                LastUpdatedAt = now
            }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager { Id = 1, DepotId = 1, UserId = SeedConstants.AdminUserId, AssignedAt = now },
            new DepotManager { Id = 2, DepotId = 2, UserId = SeedConstants.CoordinatorUserId, AssignedAt = now }
        );
    }

    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyInventory>().HasData(
            // Hue Depot
            new DepotSupplyInventory { Id = 1, DepotId = 1, ReliefItemId = 1, Quantity = 2000, ReservedQuantity = 100, LastStockedAt = now },
            new DepotSupplyInventory { Id = 2, DepotId = 1, ReliefItemId = 3, Quantity = 1000, ReservedQuantity = 0, LastStockedAt = now },
            
            // Le Thuy Depot
            new DepotSupplyInventory { Id = 3, DepotId = 2, ReliefItemId = 1, Quantity = 500, ReservedQuantity = 400, LastStockedAt = now },
            new DepotSupplyInventory { Id = 4, DepotId = 2, ReliefItemId = 2, Quantity = 100, ReservedQuantity = 50, LastStockedAt = now }
        );
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<InventoryLog>().HasData(
            new InventoryLog
            {
                Id = 1,
                DepotSupplyInventoryId = 1,
                ActionType = "Import",
                QuantityChange = 2000,
                SourceType = "Organization",
                SourceId = 1,
                PerformedBy = SeedConstants.AdminUserId,
                Note = "Nhập kho đợt 1",
                CreatedAt = now
            },
            new InventoryLog
            {
                Id = 2,
                DepotSupplyInventoryId = 3,
                ActionType = "Transfer",
                QuantityChange = 500,
                SourceType = "Depot",
                SourceId = 1,
                PerformedBy = SeedConstants.CoordinatorUserId,
                Note = "Chuyển từ kho Huế ra Lệ Thủy",
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
                ReceivedDate = new DateOnly(2024, 10, 1),
                ExpiredDate = new DateOnly(2025, 10, 1),
                Notes = "Hàng cứu trợ từ TW"
            }
        );
    }
}
