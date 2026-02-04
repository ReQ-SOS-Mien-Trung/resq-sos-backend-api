using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Logistics; // Added namespace for DepotStatus
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
                // FIX: Used to be "Active", now uses Enum "Available"
                Status = DepotStatus.Available.ToString(), 
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
                // FIX: Used to be "Active", now uses Enum "Available"
                Status = DepotStatus.Available.ToString(),
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
                UserId = SeedConstants.AdminUserId,
                AssignedAt = now
            },
            new DepotManager
            {
                Id = 2,
                DepotId = 2,
                UserId = SeedConstants.CoordinatorUserId,
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
                PerformedBy = SeedConstants.AdminUserId,
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
                PerformedBy = SeedConstants.CoordinatorUserId,
                Note = "Nhập kho đợt 1",
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
