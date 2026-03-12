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
            new ReliefItem { Id = 2, CategoryId = 2, Name = "Nước tinh khiết", Unit = "chai 500ml", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 3, CategoryId = 3, Name = "Thuốc hạ sốt Paracetamol 500mg", Unit = "viên", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 4, CategoryId = 8, Name = "Áo phao cứu sinh", Unit = "chiếc", ItemType = ItemType.Equipment.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 5, CategoryId = 4, Name = "Băng vệ sinh", Unit = "miếng", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 6, CategoryId = 9, Name = "Chăn ấm giữ nhiệt", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 7, CategoryId = 1, Name = "Sữa bột trẻ em", Unit = "gói", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 8, CategoryId = 1, Name = "Lương khô", Unit = "phong", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 9, CategoryId = 3, Name = "Dầu gió", Unit = "chai 10ml", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Elderly.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 10, CategoryId = 3, Name = "Sắt & Vitamin tổng hợp", Unit = "viên", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Pregnant.ToString(), CreatedAt = now, UpdatedAt = now }
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Depot>().HasData(
            new Depot { Id = 1, Name = "Kho Cứu trợ Tỉnh Thừa Thiên Huế", Address = "15 Lê Lợi, TP. Huế", Location = new Point(107.585, 16.463) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 500000, CurrentUtilization = 150000, LastUpdatedAt = now },
            new Depot { Id = 2, Name = "Điểm Tập kết Lệ Thủy, Quảng Bình", Address = "TT. Kiến Giang, Lệ Thủy, QB", Location = new Point(106.782, 17.215) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 300000, CurrentUtilization = 120000, LastUpdatedAt = now },
            new Depot { Id = 3, Name = "Trạm Hỗ trợ Hải Lăng, Quảng Trị", Address = "Hải Lăng, Quảng Trị", Location = new Point(107.288, 16.689) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 200000, CurrentUtilization = 80000, LastUpdatedAt = now },
            new Depot { Id = 4, Name = "Kho Cứu trợ Hòa Vang, Đà Nẵng", Address = "Hòa Vang, Đà Nẵng", Location = new Point(108.005, 16.027) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 400000, CurrentUtilization = 100000, LastUpdatedAt = now },
            new Depot { Id = 5, Name = "Trung tâm Phân phối Cẩm Xuyên, Hà Tĩnh", Address = "Cẩm Xuyên, Hà Tĩnh", Location = new Point(106.012, 18.256) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 250000, CurrentUtilization = 90000, LastUpdatedAt = now },
            new Depot { Id = 6, Name = "Điểm Cứu trợ Trà My, Quảng Nam", Address = "Bắc Trà My, Quảng Nam", Location = new Point(108.192, 15.353) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 150000, CurrentUtilization = 50000, LastUpdatedAt = now },
            new Depot { Id = 7, Name = "Kho Vận chuyển Phước Sơn, Quảng Nam", Address = "Phước Sơn, Quảng Nam", Location = new Point(107.817, 15.426) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 100000, CurrentUtilization = 40000, LastUpdatedAt = now },
            new Depot { Id = 8, Name = "Kho Dự trữ Phong Điền, Thừa Thiên Huế", Address = "Phong Điền, Thừa Thiên Huế", Location = new Point(107.290, 16.635) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 200000, CurrentUtilization = 70000, LastUpdatedAt = now },
            new Depot { Id = 9, Name = "Kho Tiền phương Bố Trạch, Quảng Bình", Address = "Bố Trạch, Quảng Bình", Location = new Point(106.550, 17.616) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 300000, CurrentUtilization = 110000, LastUpdatedAt = now },
            new Depot { Id = 10, Name = "Kho Khẩn cấp Bình Sơn, Quảng Ngãi", Address = "Bình Sơn, Quảng Ngãi", Location = new Point(108.775, 15.312) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 250000, CurrentUtilization = 85000, LastUpdatedAt = now }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager { Id = 1, DepotId = 1, UserId = SeedConstants.AdminUserId, AssignedAt = now },
            new DepotManager { Id = 2, DepotId = 2, UserId = SeedConstants.CoordinatorUserId, AssignedAt = now },
            new DepotManager { Id = 3, DepotId = 3, UserId = SeedConstants.ManagerUserId, AssignedAt = now },
            new DepotManager { Id = 4, DepotId = 4, UserId = SeedConstants.RescuerUserId, AssignedAt = now },
            new DepotManager { Id = 5, DepotId = 5, UserId = SeedConstants.AdminUserId, AssignedAt = now },
            new DepotManager { Id = 6, DepotId = 6, UserId = SeedConstants.CoordinatorUserId, AssignedAt = now },
            new DepotManager { Id = 7, DepotId = 7, UserId = SeedConstants.ManagerUserId, AssignedAt = now },
            new DepotManager { Id = 8, DepotId = 8, UserId = SeedConstants.RescuerUserId, AssignedAt = now },
            new DepotManager { Id = 9, DepotId = 9, UserId = SeedConstants.AdminUserId, AssignedAt = now },
            new DepotManager { Id = 10, DepotId = 10, UserId = SeedConstants.CoordinatorUserId, AssignedAt = now }
        );
    }

    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyInventory>().HasData(
            new DepotSupplyInventory { Id = 1, DepotId = 1, ReliefItemId = 1, Quantity = 100000, ReservedQuantity = 10000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 2, DepotId = 2, ReliefItemId = 2, Quantity = 50000, ReservedQuantity = 5000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 3, DepotId = 3, ReliefItemId = 3, Quantity = 200000, ReservedQuantity = 20000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 4, DepotId = 4, ReliefItemId = 4, Quantity = 1500, ReservedQuantity = 200, LastStockedAt = now },
            new DepotSupplyInventory { Id = 5, DepotId = 5, ReliefItemId = 5, Quantity = 30000, ReservedQuantity = 3000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 6, DepotId = 6, ReliefItemId = 6, Quantity = 2000, ReservedQuantity = 500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 7, DepotId = 7, ReliefItemId = 7, Quantity = 25000, ReservedQuantity = 2000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 8, DepotId = 8, ReliefItemId = 8, Quantity = 80000, ReservedQuantity = 8000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 9, DepotId = 9, ReliefItemId = 9, Quantity = 10000, ReservedQuantity = 1000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 10, DepotId = 10, ReliefItemId = 10, Quantity = 50000, ReservedQuantity = 5000, LastStockedAt = now },
            // Depot 1 (Huế) - vật tư bổ sung
            new DepotSupplyInventory { Id = 11, DepotId = 1, ReliefItemId = 2, Quantity = 80000, ReservedQuantity = 5000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 12, DepotId = 1, ReliefItemId = 3, Quantity = 150000, ReservedQuantity = 15000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 13, DepotId = 1, ReliefItemId = 4, Quantity = 2000, ReservedQuantity = 200, LastStockedAt = now },
            new DepotSupplyInventory { Id = 14, DepotId = 1, ReliefItemId = 6, Quantity = 3000, ReservedQuantity = 300, LastStockedAt = now },
            // Depot 2 (Lệ Thủy, Quảng Bình) - vật tư bổ sung
            new DepotSupplyInventory { Id = 15, DepotId = 2, ReliefItemId = 1, Quantity = 70000, ReservedQuantity = 7000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 16, DepotId = 2, ReliefItemId = 3, Quantity = 100000, ReservedQuantity = 10000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 17, DepotId = 2, ReliefItemId = 8, Quantity = 40000, ReservedQuantity = 4000, LastStockedAt = now },
            // Depot 3 (Hải Lăng, Quảng Trị) - vật tư bổ sung
            new DepotSupplyInventory { Id = 18, DepotId = 3, ReliefItemId = 1, Quantity = 60000, ReservedQuantity = 6000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 19, DepotId = 3, ReliefItemId = 2, Quantity = 40000, ReservedQuantity = 4000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 20, DepotId = 3, ReliefItemId = 4, Quantity = 1200, ReservedQuantity = 100, LastStockedAt = now },
            new DepotSupplyInventory { Id = 21, DepotId = 3, ReliefItemId = 6, Quantity = 1500, ReservedQuantity = 150, LastStockedAt = now },
            // Depot 4 (Hòa Vang, Đà Nẵng) - vật tư bổ sung
            new DepotSupplyInventory { Id = 22, DepotId = 4, ReliefItemId = 1, Quantity = 80000, ReservedQuantity = 8000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 23, DepotId = 4, ReliefItemId = 2, Quantity = 60000, ReservedQuantity = 6000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 24, DepotId = 4, ReliefItemId = 3, Quantity = 180000, ReservedQuantity = 18000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 25, DepotId = 4, ReliefItemId = 8, Quantity = 50000, ReservedQuantity = 5000, LastStockedAt = now },
            // Depot 5 (Cẩm Xuyên, Hà Tĩnh) - vật tư bổ sung
            new DepotSupplyInventory { Id = 26, DepotId = 5, ReliefItemId = 1, Quantity = 50000, ReservedQuantity = 5000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 27, DepotId = 5, ReliefItemId = 2, Quantity = 35000, ReservedQuantity = 3500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 28, DepotId = 5, ReliefItemId = 3, Quantity = 90000, ReservedQuantity = 9000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 29, DepotId = 5, ReliefItemId = 6, Quantity = 1800, ReservedQuantity = 180, LastStockedAt = now },
            // Depot 6 (Bắc Trà My, Quảng Nam) - vật tư bổ sung
            new DepotSupplyInventory { Id = 30, DepotId = 6, ReliefItemId = 1, Quantity = 30000, ReservedQuantity = 3000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 31, DepotId = 6, ReliefItemId = 2, Quantity = 20000, ReservedQuantity = 2000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 32, DepotId = 6, ReliefItemId = 3, Quantity = 60000, ReservedQuantity = 6000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 33, DepotId = 6, ReliefItemId = 4, Quantity = 800, ReservedQuantity = 80, LastStockedAt = now },
            // Depot 7 (Phước Sơn, Quảng Nam) - vật tư bổ sung
            new DepotSupplyInventory { Id = 34, DepotId = 7, ReliefItemId = 1, Quantity = 25000, ReservedQuantity = 2500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 35, DepotId = 7, ReliefItemId = 2, Quantity = 15000, ReservedQuantity = 1500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 36, DepotId = 7, ReliefItemId = 3, Quantity = 40000, ReservedQuantity = 4000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 37, DepotId = 7, ReliefItemId = 6, Quantity = 800, ReservedQuantity = 80, LastStockedAt = now },
            // Depot 8 (Phong Điền, Thừa Thiên Huế) - vật tư bổ sung
            new DepotSupplyInventory { Id = 38, DepotId = 8, ReliefItemId = 1, Quantity = 60000, ReservedQuantity = 6000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 39, DepotId = 8, ReliefItemId = 2, Quantity = 45000, ReservedQuantity = 4500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 40, DepotId = 8, ReliefItemId = 4, Quantity = 1200, ReservedQuantity = 120, LastStockedAt = now },
            new DepotSupplyInventory { Id = 41, DepotId = 8, ReliefItemId = 3, Quantity = 100000, ReservedQuantity = 10000, LastStockedAt = now },
            // Depot 9 (Bố Trạch, Quảng Bình) - vật tư bổ sung
            new DepotSupplyInventory { Id = 42, DepotId = 9, ReliefItemId = 1, Quantity = 80000, ReservedQuantity = 8000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 43, DepotId = 9, ReliefItemId = 2, Quantity = 55000, ReservedQuantity = 5500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 44, DepotId = 9, ReliefItemId = 3, Quantity = 120000, ReservedQuantity = 12000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 45, DepotId = 9, ReliefItemId = 6, Quantity = 2000, ReservedQuantity = 200, LastStockedAt = now },
            // Depot 10 (Bình Sơn, Quảng Ngãi) - vật tư bổ sung
            new DepotSupplyInventory { Id = 46, DepotId = 10, ReliefItemId = 1, Quantity = 70000, ReservedQuantity = 7000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 47, DepotId = 10, ReliefItemId = 2, Quantity = 50000, ReservedQuantity = 5000, LastStockedAt = now },
            new DepotSupplyInventory { Id = 48, DepotId = 10, ReliefItemId = 4, Quantity = 1500, ReservedQuantity = 150, LastStockedAt = now },
            new DepotSupplyInventory { Id = 49, DepotId = 10, ReliefItemId = 3, Quantity = 130000, ReservedQuantity = 13000, LastStockedAt = now },
            // Depot 3 & 8 — vật tư đặc thù cho Cluster 3 (Phong Điền, TT-Huế)
            // SOS #4 đề cập 4 bé dưới 1 tuổi cần sữa gấp và người già hết thuốc huyết áp
            new DepotSupplyInventory { Id = 50, DepotId = 3, ReliefItemId = 7, Quantity = 5000, ReservedQuantity = 500, LastStockedAt = now },
            new DepotSupplyInventory { Id = 51, DepotId = 8, ReliefItemId = 7, Quantity = 3000, ReservedQuantity = 300, LastStockedAt = now },
            new DepotSupplyInventory { Id = 52, DepotId = 3, ReliefItemId = 9, Quantity = 2000, ReservedQuantity = 200, LastStockedAt = now },
            new DepotSupplyInventory { Id = 53, DepotId = 8, ReliefItemId = 9, Quantity = 2500, ReservedQuantity = 250, LastStockedAt = now }
        );
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<InventoryLog>().HasData(
            new InventoryLog { Id = 1, DepotSupplyInventoryId = 1, ActionType = "Import", QuantityChange = 100000, SourceType = "Organization", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập mì tôm Huế", CreatedAt = now },
            new InventoryLog { Id = 2, DepotSupplyInventoryId = 2, ActionType = "Import", QuantityChange = 50000, SourceType = "Organization", SourceId = 2, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập nước suối Lệ Thủy", CreatedAt = now },
            new InventoryLog { Id = 3, DepotSupplyInventoryId = 3, ActionType = "Import", QuantityChange = 200000, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập Paracetamol Hải Lăng", CreatedAt = now },
            new InventoryLog { Id = 4, DepotSupplyInventoryId = 4, ActionType = "Import", QuantityChange = 1500, SourceType = "Organization", SourceId = 4, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập áo phao Hòa Vang", CreatedAt = now },
            new InventoryLog { Id = 5, DepotSupplyInventoryId = 5, ActionType = "Import", QuantityChange = 30000, SourceType = "Organization", SourceId = 5, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập băng vệ sinh Cẩm Xuyên", CreatedAt = now },
            new InventoryLog { Id = 6, DepotSupplyInventoryId = 6, ActionType = "Import", QuantityChange = 2000, SourceType = "Organization", SourceId = 6, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập chăn ấm Trà My", CreatedAt = now },
            new InventoryLog { Id = 7, DepotSupplyInventoryId = 7, ActionType = "Import", QuantityChange = 25000, SourceType = "Organization", SourceId = 7, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập sữa bột Phước Sơn", CreatedAt = now },
            new InventoryLog { Id = 8, DepotSupplyInventoryId = 8, ActionType = "Import", QuantityChange = 80000, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập lương khô Phong Điền", CreatedAt = now },
            new InventoryLog { Id = 9, DepotSupplyInventoryId = 9, ActionType = "Import", QuantityChange = 10000, SourceType = "Organization", SourceId = 9, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập dầu gió Bố Trạch", CreatedAt = now },
            new InventoryLog { Id = 10, DepotSupplyInventoryId = 10, ActionType = "Import", QuantityChange = 50000, SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập Vitamin Bình Sơn", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 1
            new InventoryLog { Id = 11, DepotSupplyInventoryId = 11, ActionType = "Import", QuantityChange = 80000, SourceType = "Organization", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập nước uống bổ sung kho Huế", CreatedAt = now },
            new InventoryLog { Id = 12, DepotSupplyInventoryId = 12, ActionType = "Import", QuantityChange = 150000, SourceType = "Organization", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập Paracetamol bổ sung kho Huế", CreatedAt = now },
            new InventoryLog { Id = 13, DepotSupplyInventoryId = 13, ActionType = "Import", QuantityChange = 2000, SourceType = "Organization", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập áo phao kho Huế", CreatedAt = now },
            new InventoryLog { Id = 14, DepotSupplyInventoryId = 14, ActionType = "Import", QuantityChange = 3000, SourceType = "Organization", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập chăn ấm kho Huế", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 2
            new InventoryLog { Id = 15, DepotSupplyInventoryId = 15, ActionType = "Import", QuantityChange = 70000, SourceType = "Organization", SourceId = 2, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập mì tôm bổ sung kho Lệ Thủy", CreatedAt = now },
            new InventoryLog { Id = 16, DepotSupplyInventoryId = 16, ActionType = "Import", QuantityChange = 100000, SourceType = "Organization", SourceId = 2, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập thuốc bổ sung kho Lệ Thủy", CreatedAt = now },
            new InventoryLog { Id = 17, DepotSupplyInventoryId = 17, ActionType = "Import", QuantityChange = 40000, SourceType = "Organization", SourceId = 2, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập lương khô kho Lệ Thủy", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 3
            new InventoryLog { Id = 18, DepotSupplyInventoryId = 18, ActionType = "Import", QuantityChange = 60000, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập mì tôm kho Hải Lăng", CreatedAt = now },
            new InventoryLog { Id = 19, DepotSupplyInventoryId = 19, ActionType = "Import", QuantityChange = 40000, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập nước uống kho Hải Lăng", CreatedAt = now },
            new InventoryLog { Id = 20, DepotSupplyInventoryId = 20, ActionType = "Import", QuantityChange = 1200, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập áo phao kho Hải Lăng", CreatedAt = now },
            new InventoryLog { Id = 21, DepotSupplyInventoryId = 21, ActionType = "Import", QuantityChange = 1500, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập chăn ấm kho Hải Lăng", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 4
            new InventoryLog { Id = 22, DepotSupplyInventoryId = 22, ActionType = "Import", QuantityChange = 80000, SourceType = "Organization", SourceId = 4, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập mì tôm kho Hòa Vang", CreatedAt = now },
            new InventoryLog { Id = 23, DepotSupplyInventoryId = 23, ActionType = "Import", QuantityChange = 60000, SourceType = "Organization", SourceId = 4, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập nước uống kho Hòa Vang", CreatedAt = now },
            new InventoryLog { Id = 24, DepotSupplyInventoryId = 24, ActionType = "Import", QuantityChange = 180000, SourceType = "Organization", SourceId = 4, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập thuốc kho Hòa Vang", CreatedAt = now },
            new InventoryLog { Id = 25, DepotSupplyInventoryId = 25, ActionType = "Import", QuantityChange = 50000, SourceType = "Organization", SourceId = 4, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập lương khô kho Hòa Vang", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 5
            new InventoryLog { Id = 26, DepotSupplyInventoryId = 26, ActionType = "Import", QuantityChange = 50000, SourceType = "Organization", SourceId = 5, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập mì tôm kho Cẩm Xuyên", CreatedAt = now },
            new InventoryLog { Id = 27, DepotSupplyInventoryId = 27, ActionType = "Import", QuantityChange = 35000, SourceType = "Organization", SourceId = 5, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập nước uống kho Cẩm Xuyên", CreatedAt = now },
            new InventoryLog { Id = 28, DepotSupplyInventoryId = 28, ActionType = "Import", QuantityChange = 90000, SourceType = "Organization", SourceId = 5, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập thuốc kho Cẩm Xuyên", CreatedAt = now },
            new InventoryLog { Id = 29, DepotSupplyInventoryId = 29, ActionType = "Import", QuantityChange = 1800, SourceType = "Organization", SourceId = 5, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập chăn ấm kho Cẩm Xuyên", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 6
            new InventoryLog { Id = 30, DepotSupplyInventoryId = 30, ActionType = "Import", QuantityChange = 30000, SourceType = "Organization", SourceId = 6, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập mì tôm kho Trà My", CreatedAt = now },
            new InventoryLog { Id = 31, DepotSupplyInventoryId = 31, ActionType = "Import", QuantityChange = 20000, SourceType = "Organization", SourceId = 6, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập nước uống kho Trà My", CreatedAt = now },
            new InventoryLog { Id = 32, DepotSupplyInventoryId = 32, ActionType = "Import", QuantityChange = 60000, SourceType = "Organization", SourceId = 6, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập thuốc kho Trà My", CreatedAt = now },
            new InventoryLog { Id = 33, DepotSupplyInventoryId = 33, ActionType = "Import", QuantityChange = 800, SourceType = "Organization", SourceId = 6, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập áo phao kho Trà My", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 7
            new InventoryLog { Id = 34, DepotSupplyInventoryId = 34, ActionType = "Import", QuantityChange = 25000, SourceType = "Organization", SourceId = 7, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập mì tôm kho Phước Sơn", CreatedAt = now },
            new InventoryLog { Id = 35, DepotSupplyInventoryId = 35, ActionType = "Import", QuantityChange = 15000, SourceType = "Organization", SourceId = 7, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập nước uống kho Phước Sơn", CreatedAt = now },
            new InventoryLog { Id = 36, DepotSupplyInventoryId = 36, ActionType = "Import", QuantityChange = 40000, SourceType = "Organization", SourceId = 7, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập thuốc kho Phước Sơn", CreatedAt = now },
            new InventoryLog { Id = 37, DepotSupplyInventoryId = 37, ActionType = "Import", QuantityChange = 800, SourceType = "Organization", SourceId = 7, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập chăn ấm kho Phước Sơn", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 8
            new InventoryLog { Id = 38, DepotSupplyInventoryId = 38, ActionType = "Import", QuantityChange = 60000, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập mì tôm kho Phong Điền", CreatedAt = now },
            new InventoryLog { Id = 39, DepotSupplyInventoryId = 39, ActionType = "Import", QuantityChange = 45000, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập nước uống kho Phong Điền", CreatedAt = now },
            new InventoryLog { Id = 40, DepotSupplyInventoryId = 40, ActionType = "Import", QuantityChange = 1200, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập áo phao kho Phong Điền", CreatedAt = now },
            new InventoryLog { Id = 41, DepotSupplyInventoryId = 41, ActionType = "Import", QuantityChange = 100000, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập thuốc kho Phong Điền", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 9
            new InventoryLog { Id = 42, DepotSupplyInventoryId = 42, ActionType = "Import", QuantityChange = 80000, SourceType = "Organization", SourceId = 9, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập mì tôm kho Bố Trạch", CreatedAt = now },
            new InventoryLog { Id = 43, DepotSupplyInventoryId = 43, ActionType = "Import", QuantityChange = 55000, SourceType = "Organization", SourceId = 9, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập nước uống kho Bố Trạch", CreatedAt = now },
            new InventoryLog { Id = 44, DepotSupplyInventoryId = 44, ActionType = "Import", QuantityChange = 120000, SourceType = "Organization", SourceId = 9, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập thuốc kho Bố Trạch", CreatedAt = now },
            new InventoryLog { Id = 45, DepotSupplyInventoryId = 45, ActionType = "Import", QuantityChange = 2000, SourceType = "Organization", SourceId = 9, PerformedBy = SeedConstants.AdminUserId, Note = "Nhập chăn ấm kho Bố Trạch", CreatedAt = now },
            // Logs cho vật tư bổ sung depot 10
            new InventoryLog { Id = 46, DepotSupplyInventoryId = 46, ActionType = "Import", QuantityChange = 70000, SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập mì tôm kho Bình Sơn", CreatedAt = now },
            new InventoryLog { Id = 47, DepotSupplyInventoryId = 47, ActionType = "Import", QuantityChange = 50000, SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập nước uống kho Bình Sơn", CreatedAt = now },
            new InventoryLog { Id = 48, DepotSupplyInventoryId = 48, ActionType = "Import", QuantityChange = 1500, SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập áo phao kho Bình Sơn", CreatedAt = now },
            new InventoryLog { Id = 49, DepotSupplyInventoryId = 49, ActionType = "Import", QuantityChange = 130000, SourceType = "Organization", SourceId = 10, PerformedBy = SeedConstants.CoordinatorUserId, Note = "Nhập thuốc kho Bình Sơn", CreatedAt = now },
// Logs cho vật tư đặc thù Cluster 3 (depot 3 & 8)
            new InventoryLog { Id = 50, DepotSupplyInventoryId = 50, ActionType = "Import", QuantityChange = 5000, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập sữa bột trẻ em kho Hải Lăng — dự trữ cho trẻ dưới 1 tuổi vùng lũ", CreatedAt = now },
            new InventoryLog { Id = 51, DepotSupplyInventoryId = 51, ActionType = "Import", QuantityChange = 3000, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập sữa bột trẻ em kho Phong Điền — dự trữ khẩn cấp cho trẻ sơ sinh", CreatedAt = now },
            new InventoryLog { Id = 52, DepotSupplyInventoryId = 52, ActionType = "Import", QuantityChange = 2000, SourceType = "Organization", SourceId = 3, PerformedBy = SeedConstants.ManagerUserId, Note = "Nhập dầu gió kho Hải Lăng — hỗ trợ y tế người già bệnh nền", CreatedAt = now },
            new InventoryLog { Id = 53, DepotSupplyInventoryId = 53, ActionType = "Import", QuantityChange = 2500, SourceType = "Organization", SourceId = 8, PerformedBy = SeedConstants.RescuerUserId, Note = "Nhập dầu gió kho Phong Điền — hỗ trợ người già hết thuốc huyết áp", CreatedAt = now }
// Export samples to demonstrate negative formatting
                     new InventoryLog { Id = 54, DepotSupplyInventoryId = 1, ActionType = "Export", QuantityChange = 5000, SourceType = "Mission", SourceId = 1, PerformedBy = SeedConstants.AdminUserId, Note = "Xuất mì tôm cho nhiệm vụ cứu hộ", CreatedAt = now.AddHours(1) },
            new InventoryLog { Id = 55, DepotSupplyInventoryId = 22, ActionType = "TransferOut", QuantityChange = 2000, SourceType = "Transfer", SourceId = 2, PerformedBy = SeedConstants.ManagerUserId, Note = "Chuyển mì tôm sang kho Lệ Thủy", CreatedAt = now.AddHours(2) },
            new InventoryLog { Id = 56, DepotSupplyInventoryId = 3, ActionType = "Adjust", QuantityChange = -1000, SourceType = "Adjustment", SourceId = null, PerformedBy = SeedConstants.AdminUserId, Note = "Điều chỉnh số lượng thuốc do hết hạn", CreatedAt = now.AddHours(3) }
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
}
