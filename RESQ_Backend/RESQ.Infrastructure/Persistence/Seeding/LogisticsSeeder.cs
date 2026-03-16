using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Logistics;
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
        SeedVatInvoices(modelBuilder);
        SeedVatInvoiceItems(modelBuilder);
        SeedInventoryLogs(modelBuilder);
        SeedOrganizationReliefItems(modelBuilder);
    }

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ItemCategory>().HasData(
            new ItemCategory { Id = 1, Code = "Food", Name = "Thực phẩm", Description = "Lương thực, đồ ăn khô", CreatedAt = now },
            new ItemCategory { Id = 2, Code = "Water", Name = "Nước uống", Description = "Nước sạch, nước đóng chai", CreatedAt = now },
            new ItemCategory { Id = 3, Code = "Medical", Name = "Y tế", Description = "Thuốc men, dụng cụ sơ cứu", CreatedAt = now },
            new ItemCategory { Id = 4, Code = "Hygiene", Name = "Vệ sinh cá nhân", Description = "Khăn giấy, xà phòng, băng vệ sinh", CreatedAt = now },
            new ItemCategory { Id = 5, Code = "Clothing", Name = "Quần áo", Description = "Quần áo sạch, áo mưa", CreatedAt = now },
            new ItemCategory { Id = 6, Code = "Shelter", Name = "Nơi trú ẩn", Description = "Lều bạt, túi ngủ", CreatedAt = now },
            new ItemCategory { Id = 7, Code = "RepairTools", Name = "Công cụ sửa chữa", Description = "Búa, đinh, cưa", CreatedAt = now },
            new ItemCategory { Id = 8, Code = "RescueEquipment", Name = "Thiết bị cứu hộ", Description = "Áo phao, xuồng, dây thừng", CreatedAt = now },
            new ItemCategory { Id = 9, Code = "Heating", Name = "Sưởi ấm", Description = "Chăn, than, máy sưởi", CreatedAt = now },
            new ItemCategory { Id = 99, Code = "Others", Name = "Khác", Description = "Các vật phẩm khác", CreatedAt = now }
        );
    }

    private static void SeedOrganizations(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Organization>().HasData(
            new Organization { Id = 1, Name = "Hội Chữ Thập Đỏ - Thừa Thiên Huế", Phone = "02343822123", Email = "hue@redcross.org.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 2, Name = "Ủy ban MTTQ Việt Nam - Quảng Bình", Phone = "02323812345", Email = "mttq@quangbinh.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 3, Name = "Quỹ Tấm Lòng Vàng - Đà Nẵng", Phone = "02363567890", Email = "contact@tamlongvang-dn.org", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 4, Name = "Tỉnh Đoàn Quảng Trị", Phone = "02333852111", Email = "tinhdoan@quangtri.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 5, Name = "Hội Liên hiệp Phụ nữ - Hà Tĩnh", Phone = "02393855222", Email = "phunu@hatinh.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 6, Name = "Nhóm Thiện Nguyện Đồng Xanh - Quảng Nam", Phone = "0905123456", Email = "dongxanh@thiennguyen.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 7, Name = "Hội Chữ Thập Đỏ - Quảng Ngãi", Phone = "02553822777", Email = "quangngai@redcross.org.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 8, Name = "Ban Chỉ huy PCTT & TKCN Miền Trung", Phone = "02363822999", Email = "pctt@mientrung.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 9, Name = "Câu lạc bộ Tình Người - Phú Yên", Phone = "0988765432", Email = "tinhnguoi@phuyen.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 10, Name = "Quỹ Bảo trợ Trẻ em Miền Trung", Phone = "02343811811", Email = "treem@baotromientrung.vn", IsActive = true, CreatedAt = now, UpdatedAt = now }
        );
    }

    private static void SeedReliefItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<ReliefItem>().HasData(
            new ReliefItem { Id = 1, CategoryId = 1, Name = "Mì tôm", Unit = "gói", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 2, CategoryId = 2, Name = "Nước tinh khiết", Unit = "chai", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 3, CategoryId = 3, Name = "Thuốc hạ sốt Paracetamol 500mg", Unit = "viên", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 4, CategoryId = 8, Name = "Áo phao cứu sinh", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 5, CategoryId = 4, Name = "Băng vệ sinh", Unit = "miếng", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 6, CategoryId = 9, Name = "Chăn ấm giữ nhiệt", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 7, CategoryId = 1, Name = "Sữa bột trẻ em", Unit = "gói", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 8, CategoryId = 1, Name = "Lương khô", Unit = "phong", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 9, CategoryId = 3, Name = "Dầu gió", Unit = "chai", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Elderly.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 10, CategoryId = 3, Name = "Sắt & Vitamin tổng hợp", Unit = "viên", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Pregnant.ToString(), CreatedAt = now, UpdatedAt = now }
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Depot>().HasData(
            new Depot { Id = 1, Name = "Ủy ban Mặt trận Tổ quốc Việt Nam", Address = "46 Điện Biên Phủ, Phường Đa Kao, Quận 1, TP. Hồ Chí Minh", Location = new Point(106.6973581406628, 10.786765331782663) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 500000, CurrentUtilization = 180000, LastUpdatedAt = now },
            new Depot { Id = 2, Name = "Ủy ban Mặt trận Tổ quốc Việt Nam thành phố Đà Nẵng", Address = "10 Ông Ích Khiêm, Hải Châu, Đà Nẵng", Location = new Point(108.22283205420794, 16.080298466000496) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 400000, CurrentUtilization = 150000, LastUpdatedAt = now },
            new Depot { Id = 3, Name = "Ủy Ban Mặt Trận Tổ Quốc Tỉnh Hà Tĩnh", Address = "02 Phan Đình Phùng, TP. Hà Tĩnh", Location = new Point(105.90102499916586, 18.349622333272194) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 350000, CurrentUtilization = 120000, LastUpdatedAt = now },
            new Depot { Id = 4, Name = "Uỷ Ban Mặt Trận Tổ Quốc Việt Nam Tỉnh Thừa Thiên Huế", Address = "01 Tôn Đức Thắng, TP. Huế, Thừa Thiên Huế", Location = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 400000, CurrentUtilization = 140000, LastUpdatedAt = now }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager { Id = 1, DepotId = 1, UserId = SeedConstants.ManagerUserId,  AssignedAt = now },
            new DepotManager { Id = 2, DepotId = 2, UserId = SeedConstants.Manager2UserId, AssignedAt = now },
            new DepotManager { Id = 3, DepotId = 3, UserId = SeedConstants.Manager3UserId, AssignedAt = now },
            new DepotManager { Id = 4, DepotId = 4, UserId = SeedConstants.Manager4UserId, AssignedAt = now }
        );
    }

    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyInventory>().HasData(
            // Depot 1 — UBMTTQVN (TP. HCM) — 8 loại vật tư
            new DepotSupplyInventory { Id = 1,  DepotId = 1, ReliefItemId = 1,  Quantity = 120000, ReservedQuantity = 12000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 2,  DepotId = 1, ReliefItemId = 2,  Quantity = 90000,  ReservedQuantity = 9000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 3,  DepotId = 1, ReliefItemId = 3,  Quantity = 200000, ReservedQuantity = 20000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 4,  DepotId = 1, ReliefItemId = 4,  Quantity = 2500,   ReservedQuantity = 300,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 5,  DepotId = 1, ReliefItemId = 5,  Quantity = 40000,  ReservedQuantity = 4000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 6,  DepotId = 1, ReliefItemId = 6,  Quantity = 3500,   ReservedQuantity = 350,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 7,  DepotId = 1, ReliefItemId = 7,  Quantity = 30000,  ReservedQuantity = 3000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 8,  DepotId = 1, ReliefItemId = 8,  Quantity = 80000,  ReservedQuantity = 8000,  LastStockedAt = now },

            // Depot 2 — UBMTTQVN TP. Đà Nẵng — 8 loại vật tư
            new DepotSupplyInventory { Id = 9,  DepotId = 2, ReliefItemId = 1,  Quantity = 80000,  ReservedQuantity = 8000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 10, DepotId = 2, ReliefItemId = 2,  Quantity = 65000,  ReservedQuantity = 6500,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 11, DepotId = 2, ReliefItemId = 3,  Quantity = 150000, ReservedQuantity = 15000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 12, DepotId = 2, ReliefItemId = 4,  Quantity = 1800,   ReservedQuantity = 200,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 13, DepotId = 2, ReliefItemId = 6,  Quantity = 2500,   ReservedQuantity = 250,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 14, DepotId = 2, ReliefItemId = 8,  Quantity = 55000,  ReservedQuantity = 5500,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 15, DepotId = 2, ReliefItemId = 9,  Quantity = 12000,  ReservedQuantity = 1200,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 16, DepotId = 2, ReliefItemId = 10, Quantity = 45000,  ReservedQuantity = 4500,  LastStockedAt = now },

            // Depot 3 — UBMTTQ Hà Tĩnh — 8 loại vật tư
            new DepotSupplyInventory { Id = 17, DepotId = 3, ReliefItemId = 1,  Quantity = 70000,  ReservedQuantity = 7000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 18, DepotId = 3, ReliefItemId = 2,  Quantity = 50000,  ReservedQuantity = 5000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 19, DepotId = 3, ReliefItemId = 3,  Quantity = 100000, ReservedQuantity = 10000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 20, DepotId = 3, ReliefItemId = 5,  Quantity = 25000,  ReservedQuantity = 2500,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 21, DepotId = 3, ReliefItemId = 6,  Quantity = 2000,   ReservedQuantity = 200,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 22, DepotId = 3, ReliefItemId = 7,  Quantity = 20000,  ReservedQuantity = 2000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 23, DepotId = 3, ReliefItemId = 9,  Quantity = 8000,   ReservedQuantity = 800,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 24, DepotId = 3, ReliefItemId = 10, Quantity = 35000,  ReservedQuantity = 3500,  LastStockedAt = now },

            // Depot 4 — UBMTTQVN Thừa Thiên Huế — 8 loại vật tư
            new DepotSupplyInventory { Id = 25, DepotId = 4, ReliefItemId = 1,  Quantity = 90000,  ReservedQuantity = 9000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 26, DepotId = 4, ReliefItemId = 2,  Quantity = 70000,  ReservedQuantity = 7000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 27, DepotId = 4, ReliefItemId = 3,  Quantity = 180000, ReservedQuantity = 18000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 28, DepotId = 4, ReliefItemId = 4,  Quantity = 2000,   ReservedQuantity = 200,   LastStockedAt = now },
            new DepotSupplyInventory { Id = 29, DepotId = 4, ReliefItemId = 5,  Quantity = 30000,  ReservedQuantity = 3000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 30, DepotId = 4, ReliefItemId = 7,  Quantity = 25000,  ReservedQuantity = 2500,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 31, DepotId = 4, ReliefItemId = 8,  Quantity = 60000,  ReservedQuantity = 6000,  LastStockedAt = now },
            new DepotSupplyInventory { Id = 32, DepotId = 4, ReliefItemId = 10, Quantity = 50000,  ReservedQuantity = 5000,  LastStockedAt = now }
        );
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<InventoryLog>().HasData(
            // ── Nhập kho ban đầu — Depot 1 (HCM), PerformedBy: ManagerUserId ────────
            new InventoryLog { Id = 1,  DepotSupplyInventoryId = 1,  ActionType = "Import", QuantityChange = 120000, SourceType = "Organization", SourceId = 1,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập mì tôm — kho UBMTTQVN HCM",            CreatedAt = now },
            new InventoryLog { Id = 2,  DepotSupplyInventoryId = 2,  ActionType = "Import", QuantityChange = 90000,  SourceType = "Organization", SourceId = 1,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập nước tinh khiết — kho HCM",             CreatedAt = now },
            new InventoryLog { Id = 3,  DepotSupplyInventoryId = 3,  ActionType = "Import", QuantityChange = 200000, SourceType = "Organization", SourceId = 3,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập thuốc hạ sốt Paracetamol — kho HCM",   CreatedAt = now },
            new InventoryLog { Id = 4,  DepotSupplyInventoryId = 4,  ActionType = "Import", QuantityChange = 2500,   SourceType = "Organization", SourceId = 4,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập áo phao cứu sinh — kho HCM",           CreatedAt = now },
            new InventoryLog { Id = 5,  DepotSupplyInventoryId = 5,  ActionType = "Import", QuantityChange = 40000,  SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập băng vệ sinh — kho HCM",               CreatedAt = now },
            new InventoryLog { Id = 6,  DepotSupplyInventoryId = 6,  ActionType = "Import", QuantityChange = 3500,   SourceType = "Organization", SourceId = 6,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập chăn ấm giữ nhiệt — kho HCM",          CreatedAt = now },
            new InventoryLog { Id = 7,  DepotSupplyInventoryId = 7,  ActionType = "Import", QuantityChange = 30000,  SourceType = "Organization", SourceId = 7,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập sữa bột trẻ em — kho HCM",             CreatedAt = now },
            new InventoryLog { Id = 8,  DepotSupplyInventoryId = 8,  ActionType = "Import", QuantityChange = 80000,  SourceType = "Organization", SourceId = 8,  PerformedBy = SeedConstants.ManagerUserId,  Note = "Nhập lương khô — kho HCM",                  CreatedAt = now },

            // ── Nhập kho ban đầu — Depot 2 (Đà Nẵng), PerformedBy: Manager2UserId ──
            new InventoryLog { Id = 9,  DepotSupplyInventoryId = 9,  ActionType = "Import", QuantityChange = 80000,  SourceType = "Organization", SourceId = 3,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập mì tôm — kho UBMTTQVN Đà Nẵng",       CreatedAt = now },
            new InventoryLog { Id = 10, DepotSupplyInventoryId = 10, ActionType = "Import", QuantityChange = 65000,  SourceType = "Organization", SourceId = 3,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập nước tinh khiết — kho Đà Nẵng",        CreatedAt = now },
            new InventoryLog { Id = 11, DepotSupplyInventoryId = 11, ActionType = "Import", QuantityChange = 150000, SourceType = "Organization", SourceId = 3,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập thuốc hạ sốt — kho Đà Nẵng",          CreatedAt = now },
            new InventoryLog { Id = 12, DepotSupplyInventoryId = 12, ActionType = "Import", QuantityChange = 1800,   SourceType = "Organization", SourceId = 4,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập áo phao cứu sinh — kho Đà Nẵng",      CreatedAt = now },
            new InventoryLog { Id = 13, DepotSupplyInventoryId = 13, ActionType = "Import", QuantityChange = 2500,   SourceType = "Organization", SourceId = 6,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập chăn ấm — kho Đà Nẵng",               CreatedAt = now },
            new InventoryLog { Id = 14, DepotSupplyInventoryId = 14, ActionType = "Import", QuantityChange = 55000,  SourceType = "Organization", SourceId = 8,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập lương khô — kho Đà Nẵng",              CreatedAt = now },
            new InventoryLog { Id = 15, DepotSupplyInventoryId = 15, ActionType = "Import", QuantityChange = 12000,  SourceType = "Organization", SourceId = 9,  PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập dầu gió — kho Đà Nẵng",               CreatedAt = now },
            new InventoryLog { Id = 16, DepotSupplyInventoryId = 16, ActionType = "Import", QuantityChange = 45000,  SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.Manager2UserId, Note = "Nhập Vitamin tổng hợp — kho Đà Nẵng",       CreatedAt = now },

            // ── Nhập kho ban đầu — Depot 3 (Hà Tĩnh), PerformedBy: Manager3UserId ──
            new InventoryLog { Id = 17, DepotSupplyInventoryId = 17, ActionType = "Import", QuantityChange = 70000,  SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập mì tôm — kho UBMTTQ Hà Tĩnh",         CreatedAt = now },
            new InventoryLog { Id = 18, DepotSupplyInventoryId = 18, ActionType = "Import", QuantityChange = 50000,  SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập nước tinh khiết — kho Hà Tĩnh",        CreatedAt = now },
            new InventoryLog { Id = 19, DepotSupplyInventoryId = 19, ActionType = "Import", QuantityChange = 100000, SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập thuốc hạ sốt — kho Hà Tĩnh",          CreatedAt = now },
            new InventoryLog { Id = 20, DepotSupplyInventoryId = 20, ActionType = "Import", QuantityChange = 25000,  SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập băng vệ sinh — kho Hà Tĩnh",          CreatedAt = now },
            new InventoryLog { Id = 21, DepotSupplyInventoryId = 21, ActionType = "Import", QuantityChange = 2000,   SourceType = "Organization", SourceId = 6,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập chăn ấm — kho Hà Tĩnh",               CreatedAt = now },
            new InventoryLog { Id = 22, DepotSupplyInventoryId = 22, ActionType = "Import", QuantityChange = 20000,  SourceType = "Organization", SourceId = 7,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập sữa bột trẻ em — kho Hà Tĩnh",        CreatedAt = now },
            new InventoryLog { Id = 23, DepotSupplyInventoryId = 23, ActionType = "Import", QuantityChange = 8000,   SourceType = "Organization", SourceId = 9,  PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập dầu gió — kho Hà Tĩnh",               CreatedAt = now },
            new InventoryLog { Id = 24, DepotSupplyInventoryId = 24, ActionType = "Import", QuantityChange = 35000,  SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.Manager3UserId, Note = "Nhập Vitamin tổng hợp — kho Hà Tĩnh",       CreatedAt = now },

            // ── Nhập kho ban đầu — Depot 4 (Huế), PerformedBy: Manager4UserId ──────
            new InventoryLog { Id = 25, DepotSupplyInventoryId = 25, ActionType = "Import", QuantityChange = 90000,  SourceType = "Organization", SourceId = 1,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập mì tôm — kho UBMTTQVN TT-Huế",        CreatedAt = now },
            new InventoryLog { Id = 26, DepotSupplyInventoryId = 26, ActionType = "Import", QuantityChange = 70000,  SourceType = "Organization", SourceId = 2,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập nước tinh khiết — kho TT-Huế",         CreatedAt = now },
            new InventoryLog { Id = 27, DepotSupplyInventoryId = 27, ActionType = "Import", QuantityChange = 180000, SourceType = "Organization", SourceId = 3,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập thuốc hạ sốt — kho TT-Huế",           CreatedAt = now },
            new InventoryLog { Id = 28, DepotSupplyInventoryId = 28, ActionType = "Import", QuantityChange = 2000,   SourceType = "Organization", SourceId = 4,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập áo phao cứu sinh — kho TT-Huế",       CreatedAt = now },
            new InventoryLog { Id = 29, DepotSupplyInventoryId = 29, ActionType = "Import", QuantityChange = 30000,  SourceType = "Organization", SourceId = 5,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập băng vệ sinh — kho TT-Huế",           CreatedAt = now },
            new InventoryLog { Id = 30, DepotSupplyInventoryId = 30, ActionType = "Import", QuantityChange = 25000,  SourceType = "Organization", SourceId = 7,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập sữa bột trẻ em — kho TT-Huế",         CreatedAt = now },
            new InventoryLog { Id = 31, DepotSupplyInventoryId = 31, ActionType = "Import", QuantityChange = 60000,  SourceType = "Organization", SourceId = 8,  PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập lương khô — kho TT-Huế",               CreatedAt = now },
            new InventoryLog { Id = 32, DepotSupplyInventoryId = 32, ActionType = "Import", QuantityChange = 50000,  SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.Manager4UserId, Note = "Nhập Vitamin tổng hợp — kho TT-Huế",        CreatedAt = now },

            // ── Xuất / Điều chỉnh / Chuyển kho mẫu ─────────────────────────────────
            new InventoryLog { Id = 33, DepotSupplyInventoryId = 1,  ActionType = "Export",      QuantityChange = 5000,  SourceType = "Mission",    SourceId = 1,    PerformedBy = SeedConstants.ManagerUserId,  Note = "Xuất mì tôm cho nhiệm vụ cứu hộ lũ lụt",          CreatedAt = now.AddHours(1) },
            new InventoryLog { Id = 34, DepotSupplyInventoryId = 9,  ActionType = "TransferOut", QuantityChange = 3000,  SourceType = "Transfer",   SourceId = 4,    PerformedBy = SeedConstants.Manager2UserId, Note = "Chuyển mì tôm từ Đà Nẵng sang kho TT-Huế",        CreatedAt = now.AddHours(2) },
            new InventoryLog { Id = 35, DepotSupplyInventoryId = 25, ActionType = "TransferIn",  QuantityChange = 3000,  SourceType = "Transfer",   SourceId = 2,    PerformedBy = SeedConstants.Manager4UserId, Note = "Nhận mì tôm chuyển từ kho Đà Nẵng",               CreatedAt = now.AddHours(2) },
            new InventoryLog { Id = 36, DepotSupplyInventoryId = 3,  ActionType = "Adjust",      QuantityChange = -1000, SourceType = "Adjustment", SourceId = null, PerformedBy = SeedConstants.ManagerUserId,  Note = "Điều chỉnh giảm thuốc do hết hạn sử dụng",         CreatedAt = now.AddHours(3) },
            new InventoryLog { Id = 37, DepotSupplyInventoryId = 17, ActionType = "Export",      QuantityChange = 2000,  SourceType = "Mission",    SourceId = 2,    PerformedBy = SeedConstants.Manager3UserId, Note = "Xuất mì tôm phục vụ cứu trợ vùng lũ Hà Tĩnh",    CreatedAt = now.AddHours(4) },
            new InventoryLog { Id = 38, DepotSupplyInventoryId = 27, ActionType = "Export",      QuantityChange = 8000,  SourceType = "Mission",    SourceId = 1,    PerformedBy = SeedConstants.Manager4UserId, Note = "Xuất thuốc hạ sốt cho đội cứu hộ Miền Trung",     CreatedAt = now.AddHours(5) },

            // ── Dữ liệu lịch sử cho tính năng Xuất biểu đồ biến động tồn kho ──────
            // Depot 1 (HCM): DSI 1=mì tôm, 2=nước, 3=thuốc, 4=áo phao, 6=chăn
            // Jan 2025
            new InventoryLog { Id = 39, DepotSupplyInventoryId = 1, VatInvoiceId = 1, ActionType = "Import",      QuantityChange =  20000, SourceType = "Purchase",    SourceId = 1,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập mì tôm theo hóa đơn VAT Q1/2025",                    CreatedAt = new DateTime(2025, 1, 10,  7,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 40, DepotSupplyInventoryId = 2, VatInvoiceId = 1, ActionType = "Import",      QuantityChange =  15000, SourceType = "Purchase",    SourceId = 1,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập nước tinh khiết theo hóa đơn VAT Q1/2025",           CreatedAt = new DateTime(2025, 1, 10,  9,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 41, DepotSupplyInventoryId = 1,                   ActionType = "Export",      QuantityChange =   5000, SourceType = "Mission",     MissionId = 1,   PerformedBy = SeedConstants.ManagerUserId, Note = "Xuất mì tôm phục vụ nhiệm vụ cứu hộ lũ lụt",            CreatedAt = new DateTime(2025, 1, 15,  6, 30, 0, DateTimeKind.Utc) },
            // Jun 2025
            new InventoryLog { Id = 42, DepotSupplyInventoryId = 3,                   ActionType = "Import",      QuantityChange =  30000, SourceType = "Donation",    SourceId = 1,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhận thuốc từ Hội Chữ Thập Đỏ TT-Huế đợt 2",            CreatedAt = new DateTime(2025, 6,  5,  8,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 43, DepotSupplyInventoryId = 6,                   ActionType = "Adjust",      QuantityChange =   -500, SourceType = "Adjustment",                   PerformedBy = SeedConstants.ManagerUserId, Note = "Điều chỉnh giảm chăn ấm do kiểm kê phát hiện hỏng",      CreatedAt = new DateTime(2025, 6, 20, 10,  0, 0, DateTimeKind.Utc) },
            // Oct 2025
            new InventoryLog { Id = 44, DepotSupplyInventoryId = 4,                   ActionType = "Import",      QuantityChange =   1000, SourceType = "Donation",    SourceId = 2,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhận áo phao từ UBMTTQVN Đà Nẵng hỗ trợ",               CreatedAt = new DateTime(2025, 10, 5,  7,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 45, DepotSupplyInventoryId = 2,                   ActionType = "TransferOut", QuantityChange =   5000, SourceType = "Transfer",    SourceId = 2,    PerformedBy = SeedConstants.ManagerUserId, Note = "Chuyển nước uống sang kho Đà Nẵng hỗ trợ bão số 4",      CreatedAt = new DateTime(2025, 10, 10, 6,  0, 0, DateTimeKind.Utc) },
            // Jan 2026
            new InventoryLog { Id = 46, DepotSupplyInventoryId = 3, VatInvoiceId = 2, ActionType = "Import",      QuantityChange =  30000, SourceType = "Purchase",    SourceId = 2,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập thuốc Paracetamol theo hóa đơn VAT đầu năm 2026",   CreatedAt = new DateTime(2026, 1,  8,  8,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 47, DepotSupplyInventoryId = 6,                   ActionType = "Export",      QuantityChange =    200, SourceType = "Mission",     MissionId = 2,   PerformedBy = SeedConstants.ManagerUserId, Note = "Xuất chăn ấm cho đội cứu hộ phân phối vùng lũ",          CreatedAt = new DateTime(2026, 1, 20,  9, 30, 0, DateTimeKind.Utc) },
            // Feb 2026
            new InventoryLog { Id = 48, DepotSupplyInventoryId = 4, VatInvoiceId = 3, ActionType = "Import",      QuantityChange =    500, SourceType = "Purchase",    SourceId = 3,    PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập áo phao cứu sinh mới theo hóa đơn VAT T2/2026",     CreatedAt = new DateTime(2026, 2, 12, 10,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 49, DepotSupplyInventoryId = 4,                   ActionType = "Return",      QuantityChange =    100, SourceType = "Mission",     MissionId = 1,   PerformedBy = SeedConstants.ManagerUserId, Note = "Hoàn trả áo phao sau khi kết thúc nhiệm vụ cứu hộ",      CreatedAt = new DateTime(2026, 2, 25, 14,  0, 0, DateTimeKind.Utc) },
            // Mar 2026
            new InventoryLog { Id = 50, DepotSupplyInventoryId = 1,                   ActionType = "Import",      QuantityChange =  10000, SourceType = "Donation",    SourceId = 3,    PerformedBy = SeedConstants.ManagerUserId, Note = "Tiếp nhận mì tôm từ Quỹ Tấm Lòng Vàng Đà Nẵng",         CreatedAt = new DateTime(2026, 3,  2,  8,  0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 51, DepotSupplyInventoryId = 3,                   ActionType = "Export",      QuantityChange =   5000, SourceType = "Mission",     MissionId = 1,   PerformedBy = SeedConstants.ManagerUserId, Note = "Xuất thuốc hạ sốt cấp phát cho vùng thiên tai",          CreatedAt = new DateTime(2026, 3, 10,  7, 30, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 52, DepotSupplyInventoryId = 2,                   ActionType = "Adjust",      QuantityChange =  -2000, SourceType = "Adjustment",                   PerformedBy = SeedConstants.ManagerUserId, Note = "Điều chỉnh tồn kho nước sau kiểm kê định kỳ quý I/2026", CreatedAt = new DateTime(2026, 3, 15, 16,  0, 0, DateTimeKind.Utc) }
        );
    }

    private static void SeedOrganizationReliefItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationReliefItem>().HasData(
            new OrganizationReliefItem { Id = 1, OrganizationId = 1, ReliefItemId = 1, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2025, 4, 1), Notes = "Cứu trợ đợt 1" },
            new OrganizationReliefItem { Id = 2, OrganizationId = 2, ReliefItemId = 2, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2025, 10, 1), Notes = "Cứu trợ đợt 1" },
            new OrganizationReliefItem { Id = 3, OrganizationId = 3, ReliefItemId = 3, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2026, 10, 1), Notes = "Cứu trợ y tế" },
            new OrganizationReliefItem { Id = 4, OrganizationId = 4, ReliefItemId = 4, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = null, Notes = "Trang thiết bị Tỉnh đoàn" },
            new OrganizationReliefItem { Id = 5, OrganizationId = 5, ReliefItemId = 5, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2027, 10, 1), Notes = "Nhu yếu phẩm phụ nữ" },
            new OrganizationReliefItem { Id = 6, OrganizationId = 6, ReliefItemId = 6, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = null, Notes = "Áo lạnh mùa đông" },
            new OrganizationReliefItem { Id = 7, OrganizationId = 7, ReliefItemId = 7, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2025, 6, 1), Notes = "Dinh dưỡng trẻ em" },
            new OrganizationReliefItem { Id = 8, OrganizationId = 8, ReliefItemId = 8, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2025, 12, 1), Notes = "Lương khô khẩn cấp" },
            new OrganizationReliefItem { Id = 9, OrganizationId = 9, ReliefItemId = 9, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2028, 1, 1), Notes = "Y tế người già" },
            new OrganizationReliefItem { Id = 10, OrganizationId = 10, ReliefItemId = 10, ReceivedDate = new DateOnly(2024, 10, 1), ExpiredDate = new DateOnly(2026, 5, 1), Notes = "Bổ sung Vitamin" }
        );
    }

    private static void SeedVatInvoices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoice>().HasData(
            new VatInvoice { Id = 1, InvoiceSerial = "AA", InvoiceNumber = "0001234", SupplierName = "Công ty TNHH Hùng Phúc",         SupplierTaxCode = "0301234567", InvoiceDate = new DateOnly(2025, 1, 10), TotalAmount = 145_000_000m },
            new VatInvoice { Id = 2, InvoiceSerial = "AA", InvoiceNumber = "0001235", SupplierName = "Chuỗi Siêu thị Bigmart Huế",     SupplierTaxCode = "0305678901", InvoiceDate = new DateOnly(2026, 1,  8), TotalAmount =  60_000_000m },
            new VatInvoice { Id = 3, InvoiceSerial = "BB", InvoiceNumber = "0002001", SupplierName = "Công ty Dược phẩm Minh Châu",    SupplierTaxCode = "0302345678", InvoiceDate = new DateOnly(2026, 2, 12), TotalAmount =  75_000_000m }
        );
    }

    private static void SeedVatInvoiceItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoiceItem>().HasData(
            // Invoice 1 (Jan 2025): mì tôm + nước
            new VatInvoiceItem { Id = 1, VatInvoiceId = 1, ReliefItemId = 1, Quantity = 20000, UnitPrice =   3_500m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoiceItem { Id = 2, VatInvoiceId = 1, ReliefItemId = 2, Quantity = 15000, UnitPrice =   5_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 2 (Jan 2026): thuốc hạ sốt
            new VatInvoiceItem { Id = 3, VatInvoiceId = 2, ReliefItemId = 3, Quantity = 30000, UnitPrice =   2_000m, CreatedAt = new DateTime(2026, 1,  8, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 3 (Feb 2026): áo phao
            new VatInvoiceItem { Id = 4, VatInvoiceId = 3, ReliefItemId = 4, Quantity =   500, UnitPrice = 150_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
