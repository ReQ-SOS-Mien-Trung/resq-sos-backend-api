using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using ReliefItem = RESQ.Infrastructure.Entities.Logistics.ItemModel;
using DepotSupplyInventory = RESQ.Infrastructure.Entities.Logistics.SupplyInventory;
using ItemCategory = RESQ.Infrastructure.Entities.Logistics.Category;
using DepotReusableItem = RESQ.Infrastructure.Entities.Logistics.ReusableItem;

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
        SeedDepotReusableItems(modelBuilder);
        SeedVatInvoices(modelBuilder);
        SeedVatInvoiceItems(modelBuilder);
        SeedSupplyInventoryLots(modelBuilder);
        SeedInventoryLogs(modelBuilder);
        SeedOrganizationReliefItems(modelBuilder);
    }

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Quantity = tổng vật tư theo danh mục của TẤT CẢ kho
        // Consumable: baseQty × (1.0 + 0.8 + 0.6 + 0.9) = baseQty × 3.3
        // Reusable (phi xe): số item × 3 units/kho × 4 kho = × 12
        // Vehicle: tính từng xe theo depot factor
        modelBuilder.Entity<ItemCategory>().HasData(
            new ItemCategory { Id = 1,  Code = "Food",            Name = "Thực phẩm",         Quantity = 597300,  Description = "Lương thực, đồ ăn khô",                   CreatedAt = now },
            new ItemCategory { Id = 2,  Code = "Water",           Name = "Nước uống",         Quantity = 382800,  Description = "Nước sạch, nước đóng chai",             CreatedAt = now },
            new ItemCategory { Id = 3,  Code = "Medical",         Name = "Y tế",              Quantity = 574200,  Description = "Thuốc men, dụng cụ sơ cứu",          CreatedAt = now },
            new ItemCategory { Id = 4,  Code = "Hygiene",         Name = "Vệ sinh cá nhân",   Quantity = 242550,  Description = "Khăn giấy, xà phòng, băng vệ sinh",    CreatedAt = now },
            new ItemCategory { Id = 5,  Code = "Clothing",        Name = "Quần áo",            Quantity = 13860,   Description = "Quần áo sạch, áo mưa",                  CreatedAt = now },
            new ItemCategory { Id = 6,  Code = "Shelter",         Name = "Nơi trú ẩn",         Quantity = 18186,   Description = "Lều bạt, túi ngủ",                     CreatedAt = now },
            new ItemCategory { Id = 7,  Code = "RepairTools",     Name = "Công cụ sửa chữa",  Quantity = 23196,   Description = "Búa, đinh, cưa",                       CreatedAt = now },
            new ItemCategory { Id = 8,  Code = "RescueEquipment", Name = "Thiết bị cứu hộ",  Quantity = 168,     Description = "Áo phao, xuồng, dây thừng",              CreatedAt = now },
            new ItemCategory { Id = 9,  Code = "Heating",         Name = "Sưởi ấm",            Quantity = 32340,   Description = "Chăn, than, máy sưởi",                  CreatedAt = now },
            new ItemCategory { Id = 10, Code = "Vehicle",         Name = "Phương tiện",        Quantity = 119,     Description = "Xe cộ, phương tiện vận chuyển cứu trợ", CreatedAt = now },
            new ItemCategory { Id = 99, Code = "Others",          Name = "Khác",               Quantity = 8616,    Description = "Các vật phẩm khác",                    CreatedAt = now }
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
            // ── Category 1: Thực phẩm (Food) — 10 items ──────────────────────
            new ReliefItem { Id = 1,  CategoryId = 1, Name = "Mì tôm",                        Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 7,  CategoryId = 1, Name = "Sữa bột trẻ em",                Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 8,  CategoryId = 1, Name = "Lương khô",                     Unit = "thanh", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 11, CategoryId = 1, Name = "Gạo sấy khô",                   Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 12, CategoryId = 1, Name = "Cháo ăn liền",                  Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Elderly.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 13, CategoryId = 1, Name = "Bánh mì khô",                   Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 14, CategoryId = 1, Name = "Muối tinh",                     Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 15, CategoryId = 1, Name = "Đường cát trắng",               Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 16, CategoryId = 1, Name = "Dầu ăn thực vật",               Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 17, CategoryId = 1, Name = "Thịt hộp đóng gói",             Unit = "hộp",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },

            // ── Category 2: Nước uống (Water) — 7 items (tiêu hao, phát cho nạn nhân) ──
            new ReliefItem { Id = 2,  CategoryId = 2, Name = "Nước tinh khiết",               Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 18, CategoryId = 2, Name = "Nước lọc bình 20L",             Unit = "bình",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 19, CategoryId = 2, Name = "Viên lọc nước khẩn cấp",        Unit = "viên",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 20, CategoryId = 2, Name = "Nước đóng thùng 24 chai",       Unit = "thùng", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 22, CategoryId = 2, Name = "Nước khoáng thiên nhiên 500ml", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 25, CategoryId = 2, Name = "Nước dừa đóng hộp",             Unit = "hộp",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 26, CategoryId = 2, Name = "Bột bù điện giải ORS",          Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },

            // ── Category 3: Y tế (Medical) — 9 items (tiêu hao, cấp phát cho nạn nhân) ──
            new ReliefItem { Id = 3,  CategoryId = 3, Name = "Thuốc hạ sốt Paracetamol 500mg", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 9,  CategoryId = 3, Name = "Dầu gió",                         Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Elderly.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 10, CategoryId = 3, Name = "Sắt & Vitamin tổng hợp",          Unit = "viên",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Pregnant.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 27, CategoryId = 3, Name = "Băng gạc y tế vô khuẩn",          Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 28, CategoryId = 3, Name = "Bông gòn y tế",                   Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 29, CategoryId = 3, Name = "Thuốc kháng sinh Amoxicillin",    Unit = "viên",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 30, CategoryId = 3, Name = "Dung dịch sát khuẩn Betadine",    Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 32, CategoryId = 3, Name = "Khẩu trang y tế 3 lớp",           Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 33, CategoryId = 3, Name = "Bộ sơ cứu cơ bản",                Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),   CreatedAt = now, UpdatedAt = now },

            // ── Category 4: Vệ sinh cá nhân (Hygiene) — 10 items ─────────────
            new ReliefItem { Id = 5,  CategoryId = 4, Name = "Băng vệ sinh",              Unit = "miếng", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 34, CategoryId = 4, Name = "Xà phòng diệt khuẩn",      Unit = "bánh",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 35, CategoryId = 4, Name = "Nước rửa tay khô",          Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 36, CategoryId = 4, Name = "Khăn ướt kháng khuẩn",      Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 37, CategoryId = 4, Name = "Kem đánh răng",             Unit = "tuýp",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 38, CategoryId = 4, Name = "Bàn chải đánh răng",        Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 39, CategoryId = 4, Name = "Dầu gội đầu",               Unit = "chai",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 40, CategoryId = 4, Name = "Khăn bông tắm",             Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 41, CategoryId = 4, Name = "Giấy vệ sinh",              Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 42, CategoryId = 4, Name = "Tã dùng một lần",           Unit = "miếng", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 5: Quần áo (Clothing) — 10 items ────────────────────
            new ReliefItem { Id = 43, CategoryId = 5, Name = "Áo mưa người lớn",          Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 44, CategoryId = 5, Name = "Ủng cao su chống lũ",       Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 45, CategoryId = 5, Name = "Bộ quần áo trẻ em",         Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 46, CategoryId = 5, Name = "Áo ấm người lớn",           Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 47, CategoryId = 5, Name = "Bộ quần áo người lớn",      Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 48, CategoryId = 5, Name = "Bộ quần áo người cao tuổi", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Elderly.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 49, CategoryId = 5, Name = "Găng tay giữ ấm",           Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 50, CategoryId = 5, Name = "Tất len giữ ấm",            Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 51, CategoryId = 5, Name = "Mũ len",                    Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 52, CategoryId = 5, Name = "Áo mưa trẻ em",             Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Children.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 6: Nơi trú ẩn (Shelter) — 10 items ─────────────────
            // Tiêu hao: cấp phát cho nạn nhân trú ẩn (không bắt buộc hoàn trả)
            new ReliefItem { Id = 53, CategoryId = 6, Name = "Lều bạt cứu trợ 4 người",   Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 54, CategoryId = 6, Name = "Tấm bạt che mưa đa năng",   Unit = "tấm",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 55, CategoryId = 6, Name = "Túi ngủ giữ nhiệt",         Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 56, CategoryId = 6, Name = "Đệm hơi dã chiến",          Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 57, CategoryId = 6, Name = "Màn chống côn trùng",        Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            // Tái sử dụng: dụng cụ của cứu hộ viên (bắt buộc hoàn trả)
            new ReliefItem { Id = 58, CategoryId = 6, Name = "Bộ cọc và dây lều",          Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 59, CategoryId = 6, Name = "Tấm bạt chống thấm",        Unit = "tấm",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 60, CategoryId = 6, Name = "Dây buộc đa năng",           Unit = "cuộn",  ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 61, CategoryId = 6, Name = "Đèn LED dã chiến",           Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 62, CategoryId = 6, Name = "Nến khẩn cấp",               Unit = "cây",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },

            // ── Category 7: Công cụ sửa chữa (RepairTools) — 10 items ────────
            new ReliefItem { Id = 63, CategoryId = 7, Name = "Búa đóng đinh",                     Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 64, CategoryId = 7, Name = "Đinh các loại",                     Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 65, CategoryId = 7, Name = "Cưa tay đa năng",                   Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 66, CategoryId = 7, Name = "Tua vít 2 đầu",                     Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 67, CategoryId = 7, Name = "Kìm cắt dây",                       Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 68, CategoryId = 7, Name = "Băng keo chống thấm",               Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 69, CategoryId = 7, Name = "Dao đa năng dã chiến",              Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 70, CategoryId = 7, Name = "Xẻng tay",                          Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 71, CategoryId = 7, Name = "Bao cát chống lũ",                  Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 72, CategoryId = 7, Name = "Bộ dụng cụ sửa chữa điện cơ bản",  Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 8: Thiết bị cứu hộ (RescueEquipment) — 14 items ────
            new ReliefItem { Id = 4,  CategoryId = 8, Name = "Áo phao cứu sinh",              Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 21, CategoryId = 8, Name = "Bình lọc nước dã chiến",        Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 23, CategoryId = 8, Name = "Can đựng nước 10L",             Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 24, CategoryId = 8, Name = "Túi đựng nước linh hoạt",       Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 31, CategoryId = 8, Name = "Nhiệt kế điện tử",              Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 73, CategoryId = 8, Name = "Xuồng cao su cứu hộ",           Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 74, CategoryId = 8, Name = "Dây thừng cứu sinh 30m",        Unit = "cuộn",  ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 75, CategoryId = 8, Name = "Phao tròn cứu sinh",            Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 76, CategoryId = 8, Name = "Máy bơm nước di động",          Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 77, CategoryId = 8, Name = "Bộ đàm liên lạc dã chiến",      Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 78, CategoryId = 8, Name = "Đèn tín hiệu khẩn cấp",        Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 79, CategoryId = 8, Name = "Máy phát điện di động",         Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 80, CategoryId = 8, Name = "Cáng khiêng thương",            Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 81, CategoryId = 8, Name = "Mũ bảo hiểm cứu hộ",           Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 9: Sưởi ấm (Heating) — 10 items ────────────────────
            new ReliefItem { Id = 6,  CategoryId = 9, Name = "Chăn ấm giữ nhiệt",             Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 82, CategoryId = 9, Name = "Than tổ ong",                    Unit = "viên",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 83, CategoryId = 9, Name = "Máy sưởi điện mini",             Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 84, CategoryId = 9, Name = "Túi sưởi ấm tay dùng một lần",  Unit = "gói",   ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 85, CategoryId = 9, Name = "Bộ quần áo nhiệt",               Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 86, CategoryId = 9, Name = "Ấm đun nước du lịch",            Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 87, CategoryId = 9, Name = "Bếp gas du lịch mini",           Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 88, CategoryId = 9, Name = "Bình gas mini dã chiến",         Unit = "bình",  ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 89, CategoryId = 9, Name = "Chăn điện sưởi",                 Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 90, CategoryId = 9, Name = "Tấm sưởi ấm bức xạ",            Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 10: Phương tiện (Vehicle) — 10 items ─────────────────
            new ReliefItem { Id = 101, CategoryId = 10, Name = "Xe tải cứu trợ 2.5 tấn",       Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 102, CategoryId = 10, Name = "Xe cứu thương",                 Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 103, CategoryId = 10, Name = "Xe bán tải 4x4",                Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 104, CategoryId = 10, Name = "Xe máy địa hình",               Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 105, CategoryId = 10, Name = "Ca nô cứu hộ",                  Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 106, CategoryId = 10, Name = "Xe chở hàng nhẹ 1 tấn",         Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 107, CategoryId = 10, Name = "Xe tải đông lạnh 3.5 tấn",      Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 108, CategoryId = 10, Name = "Xe khách 16 chỗ",               Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 109, CategoryId = 10, Name = "Xe cẩu di động",                Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 110, CategoryId = 10, Name = "Xe chuyên dụng phòng cháy",     Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 99: Khác (Others) — 10 items ────────────────────────
            new ReliefItem { Id = 91,  CategoryId = 99, Name = "Pin dự phòng 10000mAh",           Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 92,  CategoryId = 99, Name = "Cáp sạc đa năng",                 Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 93,  CategoryId = 99, Name = "Bản đồ địa hình khẩn cấp",        Unit = "tờ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 94,  CategoryId = 99, Name = "Còi báo động khẩn cấp",           Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 95,  CategoryId = 99, Name = "Kính bảo hộ lao động",            Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 96,  CategoryId = 99, Name = "Ba lô khẩn cấp",                  Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 97,  CategoryId = 99, Name = "Sổ tay và bút ghi chép",          Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.General.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 98,  CategoryId = 99, Name = "Bộ đèn pin đội đầu",              Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 99,  CategoryId = 99, Name = "Áo phản quang an toàn",           Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 100, CategoryId = 99, Name = "Pháo sáng khẩn cấp",              Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), TargetGroup = TargetGroup.Rescuer.ToString(),  CreatedAt = now, UpdatedAt = now }
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // CurrentUtilization = tổng consumable qty (factor × 573600) + 84 reusable phi xe + vehicle units
        // Depot 1 (×1.0): 573600 + 84 + 40 = 573724   | Depot 2 (×0.8): 458880 + 84 + 29 = 458993
        // Depot 3 (×0.6): 344160 + 84 + 20 = 344264   | Depot 4 (×0.9): 516240 + 84 + 30 = 516354
        modelBuilder.Entity<Depot>().HasData(
            new Depot { Id = 1, Name = "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", Address = "46 Đống Đa, TP. Huế, Thừa Thiên Huế", Location = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 750000, CurrentUtilization = 573724, LastUpdatedAt = now },
            new Depot { Id = 2, Name = "Ủy ban MTTQVN TP Đà Nẵng", Address = "270 Trưng Nữ Vương, Hải Châu, Đà Nẵng", Location = new Point(108.22283205420794, 16.080298466000496) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 600000, CurrentUtilization = 458993, LastUpdatedAt = now },
            new Depot { Id = 3, Name = "Ủy Ban MTTQ Tỉnh Hà Tĩnh", Address = "72 Phan Đình Phùng, TP. Hà Tĩnh, Hà Tĩnh", Location = new Point(105.90102499916586, 18.349622333272194) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 450000, CurrentUtilization = 344264, LastUpdatedAt = now },
            new Depot { Id = 4, Name = "Ủy ban MTTQVN Việt Nam", Address = "46 Tràng Thi, Hoàn Kiếm, Hà Nội", Location = new Point(106.6973581406628, 10.786765331782663) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 680000, CurrentUtilization = 516354, LastUpdatedAt = now }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager { Id = 1, DepotId = 1, UserId = SeedConstants.ManagerUserId,   AssignedAt = now },
            new DepotManager { Id = 2, DepotId = 2, UserId = SeedConstants.Manager2UserId,  AssignedAt = now },
            new DepotManager { Id = 3, DepotId = 3, UserId = SeedConstants.Manager3UserId,  AssignedAt = now },
            new DepotManager { Id = 4, DepotId = 4, UserId = SeedConstants.Manager4UserId,  AssignedAt = now }
        );
    }

    // ── Consumable items → tracked by quantity in depot_supply_inventory ──────
    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // 72 consumable relief item IDs (sorted)
        int[] consumableIds =
        {
            1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
            18, 19, 20, 22, 25, 26,
            27, 28, 29, 30, 32, 33,
            34, 35, 36, 37, 38, 39, 40, 41, 42,
            43, 44, 45, 46, 47, 48, 49, 50, 51, 52,
            53, 54, 55, 56, 57, 59, 62,
            64, 68,
            82, 83, 84, 85, 86, 87, 88, 89, 90,
            91, 92, 93, 94, 96, 97, 100
        };

        // Base quantities matching the order above
        int[] baseQty =
        {
            50000, 40000, 80000, 15000, 500, 8000, 30000, 5000, 20000, 20000, 15000, 18000, 10000, 12000, 8000, 10000,
            5000, 20000, 3000, 25000, 8000, 15000,
            10000, 8000, 12000, 6000, 30000, 3000,
            8000, 6000, 10000, 5000, 5000, 4000, 500, 8000, 12000,
            500, 300, 200, 400, 300, 200, 500, 1000, 500, 300,
            50, 100, 100, 50, 100, 100, 5000,
            3000, 4000,
            2000, 50, 5000, 200, 300, 200, 1500, 30, 20,
            200, 300, 500, 200, 100, 1000, 300
        };

        double[] factors = { 1.0, 0.8, 0.6, 0.9 };
        int[] depotIds = { 1, 2, 3, 4 };

        var list = new List<DepotSupplyInventory>();
        int id = 1;

        for (int d = 0; d < depotIds.Length; d++)
        {
            for (int i = 0; i < consumableIds.Length; i++)
            {
                int qty = (int)(baseQty[i] * factors[d]);
                list.Add(new DepotSupplyInventory
                {
                    Id = id,
                    DepotId = depotIds[d],
                    ItemModelId = consumableIds[i],
                    Quantity = qty,
                    ReservedQuantity = qty / 10,
                    LastStockedAt = now
                });
                id++;
            }
        }

        modelBuilder.Entity<DepotSupplyInventory>().HasData(list.ToArray());
    }

    // ── Reusable items → each physical unit tracked individually ──────────────
    private static void SeedDepotReusableItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        var available = ReusableItemStatus.Available.ToString();
        var good = ReusableItemCondition.Good.ToString();
        var fair = ReusableItemCondition.Fair.ToString();

        // ── Non-vehicle reusable relief item IDs (28 items) ──
        int[] reusableIds =
        {
            4, 21, 23, 24, 31,
            58, 60, 61,
            63, 65, 66, 67, 69, 70, 71, 72,
            73, 74, 75, 76, 77, 78, 79, 80, 81,
            95, 98, 99
        };

        // ── Vehicle relief item IDs (Cat 10) & base unit counts per depot ──
        // 101 Xe tải 2.5T, 102 Xe cứu thương, 103 Xe bán tải 4×4, 104 Xe máy địa hình,
        // 105 Ca nô, 106 Xe chở hàng 1T, 107 Xe đông lạnh, 108 Xe khách 16 chỗ,
        // 109 Xe cẩu, 110 Xe PCCC
        int[] vehicleIds       = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
        int[] vehicleBaseUnits = {   5,   3,   5,   8,   4,   5,   3,   3,   2,   2 };

        int[] depotIds = { 1, 2, 3, 4 };
        double[] depotFactors = { 1.0, 0.8, 0.6, 0.9 };
        string[] conditions3 = { good, good, fair };

        var list = new List<DepotReusableItem>();
        int id = 1;

        for (int d = 0; d < depotIds.Length; d++)
        {
            // ── Non-vehicle reusable items: 3 units each ──
            for (int r = 0; r < reusableIds.Length; r++)
            {
                for (int u = 1; u <= 3; u++)
                {
                    list.Add(new DepotReusableItem
                    {
                        Id = id++,
                        DepotId = depotIds[d],
                        ItemModelId = reusableIds[r],
                        SerialNumber = $"D{depotIds[d]}-R{reusableIds[r]:D3}-{u:D3}",
                        Status = available,
                        Condition = conditions3[u - 1],
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            // ── Vehicle items: variable units per type, scaled by depot factor ──
            for (int v = 0; v < vehicleIds.Length; v++)
            {
                int units = Math.Max(1, (int)(vehicleBaseUnits[v] * depotFactors[d]));
                for (int u = 1; u <= units; u++)
                {
                    list.Add(new DepotReusableItem
                    {
                        Id = id++,
                        DepotId = depotIds[d],
                        ItemModelId = vehicleIds[v],
                        SerialNumber = $"D{depotIds[d]}-V{vehicleIds[v]:D3}-{u:D3}",
                        Status = available,
                        Condition = u <= Math.Max(1, units * 2 / 3) ? good : fair,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
        }

        modelBuilder.Entity<DepotReusableItem>().HasData(list.ToArray());
    }

    // ── Lots for consumable imports ─────────────────────────────────────────
    private static void SeedSupplyInventoryLots(ModelBuilder modelBuilder)
    {
        // Each Import inventory-log gets a corresponding lot.
        // Lot Id == InventoryLog Id for simplicity (they share the same auto-sequence space in seed only).
        // RemainingQuantity = QuantityChange for seed data (nothing consumed yet).
        //
        // Log Id → DSI Id, Qty, SourceType, SourceId, ReceivedDate, ExpiredDate
        // We give realistic expiry dates: food ~6-12 months, medicine ~2 years, toiletries ~18 months, etc.

        var baseDate = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        var lots = new List<SupplyInventoryLot>
        {
            // ── Initial import (Depot 1 — Huế) ─────────────────────────────
            new() { Id = 1,  SupplyInventoryId = 1,  Quantity = 50000, RemainingQuantity = 50000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  CreatedAt = baseDate },
            new() { Id = 2,  SupplyInventoryId = 2,  Quantity = 40000, RemainingQuantity = 40000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  CreatedAt = baseDate },
            new() { Id = 3,  SupplyInventoryId = 3,  Quantity = 80000, RemainingQuantity = 80000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 4,  SupplyInventoryId = 4,  Quantity = 15000, RemainingQuantity = 15000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 5,  SupplyInventoryId = 5,  Quantity = 8000,  RemainingQuantity = 8000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 6,  SupplyInventoryId = 6,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 7,  SupplyInventoryId = 7,  Quantity = 5000,  RemainingQuantity = 5000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 8,  SupplyInventoryId = 8,  Quantity = 20000, RemainingQuantity = 20000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // ── Initial import (Depot 2 — Đà Nẵng) ─────────────────────────
            new() { Id = 9,  SupplyInventoryId = 45, Quantity = 40000, RemainingQuantity = 40000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 10, SupplyInventoryId = 46, Quantity = 32000, RemainingQuantity = 32000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 11, SupplyInventoryId = 47, Quantity = 64000, RemainingQuantity = 64000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 12, SupplyInventoryId = 48, Quantity = 12000, RemainingQuantity = 12000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 13, SupplyInventoryId = 49, Quantity = 6400,  RemainingQuantity = 6400,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 14, SupplyInventoryId = 50, Quantity = 24000, RemainingQuantity = 24000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },

            // ── Initial import (Depot 3 — Hà Tĩnh) ─────────────────────────
            new() { Id = 15, SupplyInventoryId = 89,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 16, SupplyInventoryId = 90,  Quantity = 24000, RemainingQuantity = 24000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2,  CreatedAt = baseDate },
            new() { Id = 17, SupplyInventoryId = 91,  Quantity = 48000, RemainingQuantity = 48000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 18, SupplyInventoryId = 92,  Quantity = 9000,  RemainingQuantity = 9000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 19, SupplyInventoryId = 93,  Quantity = 4800,  RemainingQuantity = 4800,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },
            new() { Id = 20, SupplyInventoryId = 95,  Quantity = 3000,  RemainingQuantity = 3000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 21, SupplyInventoryId = 96,  Quantity = 12000, RemainingQuantity = 12000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // ── Initial import (Depot 4 — MTTQVN) ──────────────────────────
            new() { Id = 22, SupplyInventoryId = 133, Quantity = 45000, RemainingQuantity = 45000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 23, SupplyInventoryId = 134, Quantity = 36000, RemainingQuantity = 36000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 24, SupplyInventoryId = 135, Quantity = 72000, RemainingQuantity = 72000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 25, SupplyInventoryId = 136, Quantity = 13500, RemainingQuantity = 13500, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 26, SupplyInventoryId = 137, Quantity = 7200,  RemainingQuantity = 7200,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },
            new() { Id = 27, SupplyInventoryId = 138, Quantity = 27000, RemainingQuantity = 27000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 28, SupplyInventoryId = 139, Quantity = 4500,  RemainingQuantity = 4500,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 29, SupplyInventoryId = 140, Quantity = 18000, RemainingQuantity = 18000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // ── Purchase imports (Depot 1 — Huế) ───────────────────────────
            // Log 33: Invoice 1, mì tôm, Jan 2025
            new() { Id = 30, SupplyInventoryId = 1,  Quantity = 20000, RemainingQuantity = 20000, ReceivedDate = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc) },
            // Log 34: Invoice 1, nước, Jan 2025
            new() { Id = 31, SupplyInventoryId = 2,  Quantity = 15000, RemainingQuantity = 15000, ReceivedDate = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc) },

            // ── Donation imports ────────────────────────────────────────────
            // Log 36: thuốc, Jun 2025
            new() { Id = 32, SupplyInventoryId = 3,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 6, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc) },
            // Log 38: sữa bột, Oct 2025
            new() { Id = 33, SupplyInventoryId = 5,  Quantity = 1000,  RemainingQuantity = 1000,  ReceivedDate = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, CreatedAt = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc) },

            // ── Purchase imports (Jan & Feb 2026) ───────────────────────────
            // Log 40: Invoice 2, thuốc, Jan 2026
            new() { Id = 34, SupplyInventoryId = 3,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2028, 1, 8, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 2, CreatedAt = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc) },
            // Log 42: Invoice 3, dầu gió, Feb 2026
            new() { Id = 35, SupplyInventoryId = 7,  Quantity = 500,   RemainingQuantity = 500,   ReceivedDate = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2029, 2, 12, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 3, CreatedAt = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc) },

            // ── Donation import (Mar 2026) ──────────────────────────────────
            // Log 44: mì tôm, Mar 2026
            new() { Id = 36, SupplyInventoryId = 1,  Quantity = 10000, RemainingQuantity = 10000, ReceivedDate = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 3, 2, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc) },
        };

        modelBuilder.Entity<SupplyInventoryLot>().HasData(lots.ToArray());
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        // DSI layout: 44 consumable items per depot, sequential IDs
        // Depot 1: DSI 1-44, Depot 2: DSI 45-88, Depot 3: DSI 89-132, Depot 4: DSI 133-176
        // Index 0=mì tôm(1), 1=nước(2), 2=thuốc(3), 3=băng VS(5), 4=sữa bột(7), 5=lương khô(8), 6=dầu gió(9), 7=vitamin(10)

        var now = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        Guid mgr1 = SeedConstants.ManagerUserId;
        Guid mgr2 = SeedConstants.Manager2UserId;
        Guid mgr3 = SeedConstants.Manager3UserId;
        Guid mgr4 = SeedConstants.Manager4UserId;

        // Expiry dates matching the lots – food ~12m, water ~18m, medicine ~24m, toiletries ~18m, milk ~6m, ration ~24m, oil ~36m, vitamin ~18m
        var exp12 = now.AddMonths(12);
        var exp18 = now.AddMonths(18);
        var exp24 = now.AddMonths(24);
        var exp06 = now.AddMonths(6);
        var exp36 = now.AddMonths(36);

        modelBuilder.Entity<InventoryLog>().HasData(
            // ── Nhập kho ban đầu ──────────────────────────────────────────────
            // Depot 1 (Huế) — DSI 1-8
            new InventoryLog { Id = 1,  DepotSupplyInventoryId = 1,  SupplyInventoryLotId = 1,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 50000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  PerformedBy = mgr1, Note = "Nhập mì tôm kho Huế từ Hội CTĐ TT-Huế",        ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 2,  DepotSupplyInventoryId = 2,  SupplyInventoryLotId = 2,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 40000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  PerformedBy = mgr1, Note = "Nhập nước uống kho Huế",                        ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 3,  DepotSupplyInventoryId = 3,  SupplyInventoryLotId = 3,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 80000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr1, Note = "Nhập thuốc Paracetamol kho Huế",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 4,  DepotSupplyInventoryId = 4,  SupplyInventoryLotId = 4,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 15000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr1, Note = "Nhập băng vệ sinh kho Huế",                     ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 5,  DepotSupplyInventoryId = 5,  SupplyInventoryLotId = 5,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 8000,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr1, Note = "Nhập sữa bột trẻ em kho Huế",                   ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 6,  DepotSupplyInventoryId = 6,  SupplyInventoryLotId = 6,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr1, Note = "Nhập lương khô kho Huế",                        ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 7,  DepotSupplyInventoryId = 7,  SupplyInventoryLotId = 7,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 5000,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr1, Note = "Nhập dầu gió kho Huế",                          ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 8,  DepotSupplyInventoryId = 8,  SupplyInventoryLotId = 8,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 20000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr1, Note = "Nhập Vitamin tổng hợp kho Huế",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // Depot 2 (Đà Nẵng) — DSI 45-50
            new InventoryLog { Id = 9,  DepotSupplyInventoryId = 45, SupplyInventoryLotId = 9,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 40000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nhập mì tôm kho Đà Nẵng từ Quỹ Tấm Lòng Vàng", ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 10, DepotSupplyInventoryId = 46, SupplyInventoryLotId = 10, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 32000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nhập nước uống kho Đà Nẵng",                    ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 11, DepotSupplyInventoryId = 47, SupplyInventoryLotId = 11, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 64000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nhập thuốc hạ sốt kho Đà Nẵng",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 12, DepotSupplyInventoryId = 48, SupplyInventoryLotId = 12, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 12000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr2, Note = "Nhập băng vệ sinh kho Đà Nẵng",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 13, DepotSupplyInventoryId = 49, SupplyInventoryLotId = 13, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 6400,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr2, Note = "Nhập sữa bột kho Đà Nẵng",                     ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 14, DepotSupplyInventoryId = 50, SupplyInventoryLotId = 14, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 24000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr2, Note = "Nhập lương khô kho Đà Nẵng từ Ban PCTT",       ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },

            // Depot 3 (Hà Tĩnh) — DSI 89-96
            new InventoryLog { Id = 15, DepotSupplyInventoryId = 89,  SupplyInventoryLotId = 15, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nhập mì tôm kho Hà Tĩnh từ Hội LHPN",          ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 16, DepotSupplyInventoryId = 90,  SupplyInventoryLotId = 16, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 24000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 2,  PerformedBy = mgr3, Note = "Nhập nước uống kho Hà Tĩnh từ MTTQ QB",        ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 17, DepotSupplyInventoryId = 91,  SupplyInventoryLotId = 17, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 48000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nhập thuốc kho Hà Tĩnh",                       ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 18, DepotSupplyInventoryId = 92,  SupplyInventoryLotId = 18, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 9000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nhập băng vệ sinh kho Hà Tĩnh",                ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 19, DepotSupplyInventoryId = 93,  SupplyInventoryLotId = 19, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 4800,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr3, Note = "Nhập sữa bột trẻ em kho Hà Tĩnh",              ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 20, DepotSupplyInventoryId = 95,  SupplyInventoryLotId = 20, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 3000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr3, Note = "Nhập dầu gió kho Hà Tĩnh",                     ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 21, DepotSupplyInventoryId = 96,  SupplyInventoryLotId = 21, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 12000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr3, Note = "Nhập Vitamin kho Hà Tĩnh",                     ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // Depot 4 (MTTQVN) — DSI 133-140
            new InventoryLog { Id = 22, DepotSupplyInventoryId = 133, SupplyInventoryLotId = 22, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 45000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nhập mì tôm kho trung ương từ Ban PCTT",       ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 23, DepotSupplyInventoryId = 134, SupplyInventoryLotId = 23, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 36000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nhập nước uống kho trung ương",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 24, DepotSupplyInventoryId = 135, SupplyInventoryLotId = 24, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 72000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr4, Note = "Nhập thuốc kho trung ương từ CTĐ Quảng Ngãi",  ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 25, DepotSupplyInventoryId = 136, SupplyInventoryLotId = 25, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 13500, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr4, Note = "Nhập băng vệ sinh kho trung ương",              ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 26, DepotSupplyInventoryId = 137, SupplyInventoryLotId = 26, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 7200,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr4, Note = "Nhập sữa bột kho trung ương",                  ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 27, DepotSupplyInventoryId = 138, SupplyInventoryLotId = 27, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 27000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nhập lương khô kho trung ương",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 28, DepotSupplyInventoryId = 139, SupplyInventoryLotId = 28, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 4500,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr4, Note = "Nhập dầu gió kho trung ương",                   ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 29, DepotSupplyInventoryId = 140, SupplyInventoryLotId = 29, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 18000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr4, Note = "Nhập Vitamin kho trung ương",                   ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // ── Mẫu đa dạng các loại hành động (không có ReceivedDate/ExpiredDate) ──
            new InventoryLog { Id = 30, DepotSupplyInventoryId = 1,  ActionType = InventoryActionType.Export.ToString(),      QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),    MissionId = 1, PerformedBy = mgr1, Note = "Xuất mì tôm cho nhiệm vụ cứu hộ lũ lụt",              CreatedAt = now.AddHours(1) },
            new InventoryLog { Id = 31, DepotSupplyInventoryId = 45, ActionType = InventoryActionType.TransferOut.ToString(), QuantityChange = 2000,  SourceType = InventorySourceType.Transfer.ToString(),   SourceId = 1,  PerformedBy = mgr2, Note = "Chuyển mì tôm từ Đà Nẵng sang kho Huế",               CreatedAt = now.AddHours(2) },
            new InventoryLog { Id = 32, DepotSupplyInventoryId = 3,  ActionType = InventoryActionType.Adjust.ToString(),      QuantityChange = -1000, SourceType = InventorySourceType.Adjustment.ToString(),                PerformedBy = mgr1, Note = "Điều chỉnh số lượng thuốc do hết hạn",                CreatedAt = now.AddHours(3) },

            // ── Giao dịch mua sắm (VAT) ─────────────────────────────────────
            // Jan 2025
            new InventoryLog { Id = 33, DepotSupplyInventoryId = 1,  VatInvoiceId = 1, SupplyInventoryLotId = 30, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 20000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nhập mì tôm theo hóa đơn VAT Q1/2025",                  ReceivedDate = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 34, DepotSupplyInventoryId = 2,  VatInvoiceId = 1, SupplyInventoryLotId = 31, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 15000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nhập nước tinh khiết theo hóa đơn VAT Q1/2025",         ReceivedDate = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 35, DepotSupplyInventoryId = 1,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Xuất mì tôm phục vụ nhiệm vụ cứu hộ lũ lụt",         CreatedAt = new DateTime(2025, 1, 15, 6, 30, 0, DateTimeKind.Utc) },

            // Jun 2025
            new InventoryLog { Id = 36, DepotSupplyInventoryId = 3,                    SupplyInventoryLotId = 32, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nhận thuốc từ Hội Chữ Thập Đỏ Huế đợt 2",             ReceivedDate = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc),  ExpiredDate = new DateTime(2027, 6, 5, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 37, DepotSupplyInventoryId = 4,                    ActionType = InventoryActionType.Adjust.ToString(), QuantityChange = -500,  SourceType = InventorySourceType.Adjustment.ToString(),             PerformedBy = mgr1, Note = "Điều chỉnh giảm băng vệ sinh do hết hạn sử dụng",      CreatedAt = new DateTime(2025, 6, 20, 10, 0, 0, DateTimeKind.Utc) },

            // Oct 2025
            new InventoryLog { Id = 38, DepotSupplyInventoryId = 5,                    SupplyInventoryLotId = 33, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 1000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Nhận sữa bột từ MTTQ Quảng Bình hỗ trợ",              ReceivedDate = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 39, DepotSupplyInventoryId = 2,                    ActionType = InventoryActionType.TransferOut.ToString(), QuantityChange = 5000, SourceType = InventorySourceType.Transfer.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Chuyển nước uống sang kho Đà Nẵng hỗ trợ bão số 4", CreatedAt = new DateTime(2025, 10, 10, 6, 0, 0, DateTimeKind.Utc) },

            // Jan 2026
            new InventoryLog { Id = 40, DepotSupplyInventoryId = 3,  VatInvoiceId = 2, SupplyInventoryLotId = 34, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Nhập thuốc Paracetamol theo hóa đơn VAT đầu năm 2026", ReceivedDate = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc),   ExpiredDate = new DateTime(2028, 1, 8, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 41, DepotSupplyInventoryId = 4,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 200,   SourceType = InventorySourceType.Mission.ToString(),  MissionId = 2, PerformedBy = mgr1, Note = "Xuất băng vệ sinh cho đội cứu hộ phân phối vùng lũ",  CreatedAt = new DateTime(2026, 1, 20, 9, 30, 0, DateTimeKind.Utc) },

            // Feb 2026
            new InventoryLog { Id = 42, DepotSupplyInventoryId = 7,  VatInvoiceId = 3, SupplyInventoryLotId = 35, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 500,   SourceType = InventorySourceType.Purchase.ToString(), SourceId = 3, PerformedBy = mgr1, Note = "Nhập dầu gió theo hóa đơn VAT T2/2026",                ReceivedDate = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2029, 2, 12, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 43, DepotSupplyInventoryId = 6,                    ActionType = InventoryActionType.Return.ToString(), QuantityChange = 100,   SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Hoàn trả lương khô sau khi kết thúc nhiệm vụ cứu hộ", CreatedAt = new DateTime(2026, 2, 25, 14, 0, 0, DateTimeKind.Utc) },

            // Mar 2026
            new InventoryLog { Id = 44, DepotSupplyInventoryId = 1,                    SupplyInventoryLotId = 36, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 10000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, PerformedBy = mgr1, Note = "Tiếp nhận mì tôm từ Quỹ Tấm Lòng Vàng Đà Nẵng",      ReceivedDate = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc),  ExpiredDate = new DateTime(2027, 3, 2, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 45, DepotSupplyInventoryId = 3,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Xuất thuốc hạ sốt cấp phát cho vùng thiên tai",      CreatedAt = new DateTime(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 46, DepotSupplyInventoryId = 2,                    ActionType = InventoryActionType.Adjust.ToString(), QuantityChange = -2000, SourceType = InventorySourceType.Adjustment.ToString(),             PerformedBy = mgr1, Note = "Điều chỉnh tồn kho nước sau kiểm kê định kỳ quý I/2026", CreatedAt = new DateTime(2026, 3, 15, 16, 0, 0, DateTimeKind.Utc) }
        );
    }

    private static void SeedOrganizationReliefItems(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<OrganizationReliefItem>().HasData(
            new OrganizationReliefItem { Id = 1,  OrganizationId = 1,  ItemModelId = 1,  Quantity = 50000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Cứu trợ đợt 1",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 2,  OrganizationId = 2,  ItemModelId = 2,  Quantity = 40000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Cứu trợ đợt 1",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 3,  OrganizationId = 3,  ItemModelId = 3,  Quantity = 80000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Cứu trợ y tế",               CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 4,  OrganizationId = 4,  ItemModelId = 4,  Quantity = 100,   ReceivedDate = seedDate, ExpiredDate = null,                                                     Notes = "Trang thiết bị Tỉnh đoàn",       CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 5,  OrganizationId = 5,  ItemModelId = 5,  Quantity = 15000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Nhu yếu phẩm phụ nữ",          CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 6,  OrganizationId = 6,  ItemModelId = 6,  Quantity = 200,   ReceivedDate = seedDate, ExpiredDate = null,                                                     Notes = "Áo lạnh mùa đông",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 7,  OrganizationId = 7,  ItemModelId = 7,  Quantity = 8000,  ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Dinh dưỡng trẻ em",           CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 8,  OrganizationId = 8,  ItemModelId = 8,  Quantity = 30000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Lương khô khẩn cấp",           CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 9,  OrganizationId = 9,  ItemModelId = 9,  Quantity = 5000,  ReceivedDate = seedDate, ExpiredDate = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Y tế người già",               CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 10, OrganizationId = 10, ItemModelId = 10, Quantity = 20000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Bổ sung Vitamin",             CreatedAt = seedDate }
        );
    }

    private static void SeedVatInvoices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoice>().HasData(
            new VatInvoice { Id = 1, InvoiceSerial = "AA", InvoiceNumber = "0001234", SupplierName = "Công ty TNHH Hùng Phúc",         SupplierTaxCode = "0301234567", InvoiceDate = new DateOnly(2025, 1, 10), TotalAmount = 145_000_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoice { Id = 2, InvoiceSerial = "AA", InvoiceNumber = "0001235", SupplierName = "Chuỗi Siêu thị Bigmart Huế",     SupplierTaxCode = "0305678901", InvoiceDate = new DateOnly(2026, 1,  8), TotalAmount =  60_000_000m, CreatedAt = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoice { Id = 3, InvoiceSerial = "BB", InvoiceNumber = "0002001", SupplierName = "Công ty Dược phẩm Minh Châu",    SupplierTaxCode = "0302345678", InvoiceDate = new DateOnly(2026, 2, 12), TotalAmount =  75_000_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );
    }

    private static void SeedVatInvoiceItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoiceItem>().HasData(
            // Invoice 1 (Jan 2025): mì tôm + nước
            new VatInvoiceItem { Id = 1, VatInvoiceId = 1, ItemModelId = 1, Quantity = 20000, UnitPrice =   3_500m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoiceItem { Id = 2, VatInvoiceId = 1, ItemModelId = 2, Quantity = 15000, UnitPrice =   5_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 2 (Jan 2026): thuốc hạ sốt
            new VatInvoiceItem { Id = 3, VatInvoiceId = 2, ItemModelId = 3, Quantity = 30000, UnitPrice =   2_000m, CreatedAt = new DateTime(2026, 1,  8, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 3 (Feb 2026): dầu gió
            new VatInvoiceItem { Id = 4, VatInvoiceId = 3, ItemModelId = 9, Quantity =  5000, UnitPrice =  15_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
