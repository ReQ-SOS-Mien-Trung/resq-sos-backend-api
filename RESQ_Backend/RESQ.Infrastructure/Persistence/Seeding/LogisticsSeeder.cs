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
        SeedItemModelTargetGroups(modelBuilder);
        SeedDepots(modelBuilder);
        SeedDepotManagers(modelBuilder);
        SeedDepotInventories(modelBuilder);
        SeedDepotReusableItems(modelBuilder);
        SeedVatInvoices(modelBuilder);
        SeedVatInvoiceItems(modelBuilder);
        SeedSupplyInventoryLots(modelBuilder);
        SeedInventoryLogs(modelBuilder);
        SeedOrganizationReliefItems(modelBuilder);
        SeedDepotSupplyRequests(modelBuilder);
        SeedDepotSupplyRequestItems(modelBuilder);
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

        var items = new[]
        {
            // ── Category 1: Thực phẩm (Food) — 10 items ──────────────────────
            new ReliefItem { Id = 1,  CategoryId = 1, Name = "Mì tôm",                        Description = "Mì ăn liền đóng gói dùng cứu trợ khẩn cấp", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 7,  CategoryId = 1, Name = "Sữa bột trẻ em",                Description = "Sữa bột dinh dưỡng dành cho trẻ em dưới 6 tuổi", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 8,  CategoryId = 1, Name = "Lương khô",                     Description = "Lương khô năng lượng cao, bảo quản lâu dài", Unit = "thanh", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 11, CategoryId = 1, Name = "Gạo sấy khô",                   Description = "Gạo sấy khô ăn liền, chỉ cần thêm nước nóng", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 12, CategoryId = 1, Name = "Cháo ăn liền",                  Description = "Cháo ăn liền đóng gói, dễ tiêu hóa cho mọi lứa tuổi", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 13, CategoryId = 1, Name = "Bánh mì khô",                   Description = "Bánh mì khô bảo quản lâu, tiện lợi khi cứu trợ", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 14, CategoryId = 1, Name = "Muối tinh",                     Description = "Muối tinh tiêu chuẩn dùng chế biến thực phẩm", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 15, CategoryId = 1, Name = "Đường cát trắng",               Description = "Đường cát trắng tinh luyện dùng pha chế và nấu ăn", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 16, CategoryId = 1, Name = "Dầu ăn thực vật",               Description = "Dầu ăn thực vật đóng chai dùng chế biến thực phẩm", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 17, CategoryId = 1, Name = "Thịt hộp đóng gói",             Description = "Thịt hộp đóng gói bảo quản lâu, giàu dinh dưỡng", Unit = "hộp",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 2: Nước uống (Water) — 7 items (tiêu hao, phát cho nạn nhân) ──
            new ReliefItem { Id = 2,  CategoryId = 2, Name = "Nước tinh khiết",               Description = "Nước uống đóng chai 500ml phục vụ cấp phát", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 18, CategoryId = 2, Name = "Nước lọc bình 20L",             Description = "Bình nước lọc 20 lít phục vụ sinh hoạt tập thể", Unit = "bình",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 19, CategoryId = 2, Name = "Viên lọc nước khẩn cấp",        Description = "Viên lọc nước cầm tay, xử lý nước bẩn thành nước uống", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 20, CategoryId = 2, Name = "Nước đóng thùng 24 chai",       Description = "Thùng 24 chai nước uống 500ml tiện phân phối", Unit = "thùng", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 22, CategoryId = 2, Name = "Nước khoáng thiên nhiên 500ml", Description = "Nước khoáng thiên nhiên đóng chai 500ml", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 25, CategoryId = 2, Name = "Nước dừa đóng hộp",             Description = "Nước dừa tươi đóng hộp bổ sung điện giải", Unit = "hộp",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 26, CategoryId = 2, Name = "Bột bù điện giải ORS",          Description = "Bột pha bù nước và điện giải cho người mất nước", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 3: Y tế (Medical) — 9 items (tiêu hao, cấp phát cho nạn nhân) ──
            new ReliefItem { Id = 3,  CategoryId = 3, Name = "Thuốc hạ sốt Paracetamol 500mg", Description = "Thuốc hạ sốt giảm đau cơ bản cho người lớn", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 9,  CategoryId = 3, Name = "Dầu gió",                         Description = "Dầu gió xanh dùng xoa bóp giảm đau, chống cảm", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 10, CategoryId = 3, Name = "Sắt & Vitamin tổng hợp",          Description = "Viên uống bổ sung sắt và vitamin tổng hợp", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 27, CategoryId = 3, Name = "Băng gạc y tế vô khuẩn",          Description = "Băng gạc vô khuẩn dùng băng bó vết thương", Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 28, CategoryId = 3, Name = "Bông gòn y tế",                   Description = "Bông gòn y tế vô khuẩn dùng vệ sinh và sơ cứu", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 29, CategoryId = 3, Name = "Thuốc kháng sinh Amoxicillin",    Description = "Thuốc kháng sinh phổ rộng điều trị nhiễm khuẩn", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 30, CategoryId = 3, Name = "Dung dịch sát khuẩn Betadine",    Description = "Dung dịch sát khuẩn Povidone-Iodine rửa vết thương", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 32, CategoryId = 3, Name = "Khẩu trang y tế 3 lớp",           Description = "Khẩu trang y tế dùng một lần, đóng gói vô khuẩn", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 33, CategoryId = 3, Name = "Bộ sơ cứu cơ bản",                Description = "Bộ sơ cứu gồm băng, gạc, kéo, kẹp và thuốc cơ bản", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 4: Vệ sinh cá nhân (Hygiene) — 10 items ─────────────
            new ReliefItem { Id = 5,  CategoryId = 4, Name = "Băng vệ sinh",              Description = "Băng vệ sinh phụ nữ dùng một lần, đóng gói riêng", Unit = "miếng", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 34, CategoryId = 4, Name = "Xà phòng diệt khuẩn",      Description = "Xà phòng cục diệt khuẩn dùng vệ sinh cá nhân", Unit = "bánh",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 35, CategoryId = 4, Name = "Nước rửa tay khô",          Description = "Gel rửa tay khô diệt khuẩn nhanh, không cần nước", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 36, CategoryId = 4, Name = "Khăn ướt kháng khuẩn",      Description = "Khăn ướt kháng khuẩn tiện dụng, đóng gói 10 tờ", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 37, CategoryId = 4, Name = "Kem đánh răng",             Description = "Kem đánh răng kích thước nhỏ gọn phù hợp cứu trợ", Unit = "tuýp",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 38, CategoryId = 4, Name = "Bàn chải đánh răng",        Description = "Bàn chải đánh răng dùng một lần, đóng gói riêng", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 39, CategoryId = 4, Name = "Dầu gội đầu",               Description = "Dầu gội đầu gói nhỏ tiện lợi cho cứu trợ", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 40, CategoryId = 4, Name = "Khăn bông tắm",             Description = "Khăn bông tắm cỡ trung dùng vệ sinh cá nhân", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 41, CategoryId = 4, Name = "Giấy vệ sinh",              Description = "Giấy vệ sinh cuộn nhỏ tiêu chuẩn", Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 42, CategoryId = 4, Name = "Tã dùng một lần",           Description = "Tã giấy dùng một lần cho trẻ em hoặc người già", Unit = "miếng", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 5: Quần áo (Clothing) — 10 items ────────────────────
            new ReliefItem { Id = 43, CategoryId = 5, Name = "Áo mưa người lớn",          Description = "Áo mưa nhựa dùng một lần cho người lớn", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 44, CategoryId = 5, Name = "Ủng cao su chống lũ",       Description = "Ủng cao su chống nước dùng đi lại trong vùng ngập", Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 45, CategoryId = 5, Name = "Bộ quần áo trẻ em",         Description = "Bộ quần áo sạch kích thước trẻ em 3–12 tuổi", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 46, CategoryId = 5, Name = "Áo ấm người lớn",           Description = "Áo khoác giữ ấm dùng trong thời tiết lạnh", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 47, CategoryId = 5, Name = "Bộ quần áo người lớn",      Description = "Bộ quần áo sạch kích thước người lớn", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 48, CategoryId = 5, Name = "Bộ quần áo người cao tuổi", Description = "Bộ quần áo thoải mái phù hợp người cao tuổi", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 49, CategoryId = 5, Name = "Găng tay giữ ấm",           Description = "Găng tay len giữ ấm trong thời tiết lạnh", Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 50, CategoryId = 5, Name = "Tất len giữ ấm",            Description = "Tất len dày giữ ấm chân trong mùa lạnh", Unit = "đôi",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 51, CategoryId = 5, Name = "Mũ len",                    Description = "Mũ len giữ ấm đầu trong thời tiết lạnh", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 52, CategoryId = 5, Name = "Áo mưa trẻ em",             Description = "Áo mưa nhựa dùng một lần cho trẻ em", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 6: Nơi trú ẩn (Shelter) — 10 items ─────────────────
            // Tiêu hao: cấp phát cho nạn nhân trú ẩn (không bắt buộc hoàn trả)
            new ReliefItem { Id = 53, CategoryId = 6, Name = "Lều bạt cứu trợ 4 người",   Description = "Lều bạt dã chiến sức chứa 4 người, chống nước", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 54, CategoryId = 6, Name = "Tấm bạt che mưa đa năng",   Description = "Tấm bạt PE chống nước đa năng dùng che mưa nắng", Unit = "tấm",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 55, CategoryId = 6, Name = "Túi ngủ giữ nhiệt",         Description = "Túi ngủ cách nhiệt dùng trong thời tiết lạnh", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 56, CategoryId = 6, Name = "Đệm hơi dã chiến",          Description = "Đệm hơi gấp gọn dùng ngủ dã chiến", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 57, CategoryId = 6, Name = "Màn chống côn trùng",        Description = "Màn lưới chống muỗi và côn trùng khi ngủ", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            // Tái sử dụng: dụng cụ của cứu hộ viên (bắt buộc hoàn trả)
            new ReliefItem { Id = 58, CategoryId = 6, Name = "Bộ cọc và dây lều",          Description = "Bộ cọc kim loại và dây buộc để dựng lều", Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 59, CategoryId = 6, Name = "Tấm bạt chống thấm",        Description = "Tấm bạt PE dày chống thấm nước dùng lót sàn lều", Unit = "tấm",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 60, CategoryId = 6, Name = "Dây buộc đa năng",           Description = "Dây thừng đa năng dùng buộc, cố định vật dụng", Unit = "cuộn",  ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 61, CategoryId = 6, Name = "Đèn LED dã chiến",           Description = "Đèn LED sạc dùng chiếu sáng dã chiến", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 62, CategoryId = 6, Name = "Nến khẩn cấp",               Description = "Nến cháy lâu dùng chiếu sáng khi mất điện", Unit = "cây",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 7: Công cụ sửa chữa (RepairTools) — 10 items ────────
            new ReliefItem { Id = 63, CategoryId = 7, Name = "Búa đóng đinh",                     Description = "Búa sắt đóng đinh dùng sửa chữa nhà cửa", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 64, CategoryId = 7, Name = "Đinh các loại",                     Description = "Bộ đinh sắt các kích cỡ dùng sửa chữa", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 65, CategoryId = 7, Name = "Cưa tay đa năng",                   Description = "Cưa tay gấp gọn dùng cắt gỗ và vật liệu", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 66, CategoryId = 7, Name = "Tua vít 2 đầu",                     Description = "Tua vít 2 đầu dẹt và bake dùng sửa chữa", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 67, CategoryId = 7, Name = "Kìm cắt dây",                       Description = "Kìm cắt dây thép và dây điện đa năng", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 68, CategoryId = 7, Name = "Băng keo chống thấm",               Description = "Băng keo dán chống thấm nước cho mái và tường", Unit = "cuộn",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 69, CategoryId = 7, Name = "Dao đa năng dã chiến",              Description = "Dao gấp đa năng tích hợp nhiều công cụ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 70, CategoryId = 7, Name = "Xẻng tay",                          Description = "Xẻng tay gấp gọn dùng đào đắp trong cứu trợ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 71, CategoryId = 7, Name = "Bao cát chống lũ",                  Description = "Bao cát dùng đắp đê ngăn nước lũ tràn", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 72, CategoryId = 7, Name = "Bộ dụng cụ sửa chữa điện cơ bản",  Description = "Bộ dụng cụ sửa chữa điện gồm kìm, tua vít, băng keo", Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },

            // ── Category 8: Thiết bị cứu hộ (RescueEquipment) — 14 items ────
            new ReliefItem { Id = 4,  CategoryId = 8, Name = "Áo phao cứu sinh",              Description = "Áo phao tiêu chuẩn phục vụ cứu hộ đường thủy", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 21, CategoryId = 8, Name = "Bình lọc nước dã chiến",        Description = "Bình lọc nước di động lọc nước bẩn thành nước sạch", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 23, CategoryId = 8, Name = "Can đựng nước 10L",             Description = "Can nhựa 10 lít chứa và vận chuyển nước sạch", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 24, CategoryId = 8, Name = "Túi đựng nước linh hoạt",       Description = "Túi nhựa dẻo đựng nước gấp gọn khi không sử dụng", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 31, CategoryId = 8, Name = "Nhiệt kế điện tử",              Description = "Nhiệt kế điện tử đo thân nhiệt nhanh chóng", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 73, CategoryId = 8, Name = "Xuồng cao su cứu hộ",           Description = "Xuồng cao su chuyên dụng cho nhiệm vụ cứu hộ lũ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 74, CategoryId = 8, Name = "Dây thừng cứu sinh 30m",        Description = "Dây thừng dài 30m chịu lực cao dùng cứu hộ", Unit = "cuộn",  ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 75, CategoryId = 8, Name = "Phao tròn cứu sinh",            Description = "Phao tròn cứu sinh tiêu chuẩn ném cho nạn nhân", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 76, CategoryId = 8, Name = "Máy bơm nước di động",          Description = "Máy bơm nước chạy xăng di động hút nước ngập", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 77, CategoryId = 8, Name = "Bộ đàm liên lạc dã chiến",      Description = "Bộ đàm cầm tay liên lạc tần số UHF/VHF", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 78, CategoryId = 8, Name = "Đèn tín hiệu khẩn cấp",        Description = "Đèn tín hiệu nhấp nháy cảnh báo khu vực nguy hiểm", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 79, CategoryId = 8, Name = "Máy phát điện di động",         Description = "Máy phát điện xăng di động công suất nhỏ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 80, CategoryId = 8, Name = "Cáng khiêng thương",            Description = "Cáng gấp gọn dùng vận chuyển người bị thương", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 81, CategoryId = 8, Name = "Mũ bảo hiểm cứu hộ",           Description = "Mũ bảo hiểm chuyên dụng cho cứu hộ viên", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },

            // ── Category 9: Sưởi ấm (Heating) — 10 items ────────────────────
            new ReliefItem { Id = 6,  CategoryId = 9, Name = "Chăn ấm giữ nhiệt",             Description = "Chăn dày giữ nhiệt dùng trong thời tiết lạnh", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 82, CategoryId = 9, Name = "Than tổ ong",                    Description = "Than tổ ong dùng đốt sưởi ấm hoặc nấu ăn", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 83, CategoryId = 9, Name = "Máy sưởi điện mini",             Description = "Máy sưởi điện nhỏ gọn công suất thấp", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 84, CategoryId = 9, Name = "Túi sưởi ấm tay dùng một lần",  Description = "Túi sưởi ấm tay phản ứng hóa học dùng một lần", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 85, CategoryId = 9, Name = "Bộ quần áo nhiệt",               Description = "Bộ đồ lót giữ nhiệt mặc trong thời tiết rét", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 86, CategoryId = 9, Name = "Ấm đun nước du lịch",            Description = "Ấm đun nước điện nhỏ gọn tiện dùng dã chiến", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 87, CategoryId = 9, Name = "Bếp gas du lịch mini",           Description = "Bếp gas mini gấp gọn dùng nấu ăn dã chiến", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 88, CategoryId = 9, Name = "Bình gas mini dã chiến",         Description = "Bình gas lon nhỏ dùng cho bếp gas du lịch", Unit = "bình",  ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 89, CategoryId = 9, Name = "Chăn điện sưởi",                 Description = "Chăn điện sưởi ấm dùng khi ngủ mùa lạnh", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 90, CategoryId = 9, Name = "Tấm sưởi ấm bức xạ",            Description = "Tấm sưởi hồng ngoại bức xạ di động", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 10: Phương tiện (Vehicle) — 10 items ─────────────────
            new ReliefItem { Id = 101, CategoryId = 10, Name = "Xe tải cứu trợ 2.5 tấn",       Description = "Xe tải 2.5 tấn vận chuyển hàng cứu trợ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 102, CategoryId = 10, Name = "Xe cứu thương",                 Description = "Xe chuyên dụng vận chuyển cấp cứu và bệnh nhân", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 103, CategoryId = 10, Name = "Xe bán tải 4x4",                Description = "Xe bán tải 2 cầu vượt địa hình xấu", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 104, CategoryId = 10, Name = "Xe máy địa hình",               Description = "Xe máy địa hình đi vào vùng khó tiếp cận", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 105, CategoryId = 10, Name = "Ca nô cứu hộ",                  Description = "Ca nô máy chuyên dụng cứu hộ đường thủy", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 106, CategoryId = 10, Name = "Xe chở hàng nhẹ 1 tấn",         Description = "Xe tải nhẹ 1 tấn vận chuyển hàng cứu trợ", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 107, CategoryId = 10, Name = "Xe tải đông lạnh 3.5 tấn",      Description = "Xe tải đông lạnh bảo quản thực phẩm tươi sống", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 108, CategoryId = 10, Name = "Xe khách 16 chỗ",               Description = "Xe khách 16 chỗ chở người sơ tán", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 109, CategoryId = 10, Name = "Xe cẩu di động",                Description = "Xe cẩu di động dọn dẹp đổ nát và vật cản", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 110, CategoryId = 10, Name = "Xe chuyên dụng phòng cháy",     Description = "Xe chữa cháy chuyên dụng phòng cháy chữa cháy", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(), CreatedAt = now, UpdatedAt = now },

            // ── Category 99: Khác (Others) — 10 items ────────────────────────
            new ReliefItem { Id = 91,  CategoryId = 99, Name = "Pin dự phòng 10000mAh",           Description = "Pin sạc dự phòng 10000mAh sạc điện thoại", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 92,  CategoryId = 99, Name = "Cáp sạc đa năng",                 Description = "Cáp sạc đa đầu Lightning/USB-C/Micro USB", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 93,  CategoryId = 99, Name = "Bản đồ địa hình khẩn cấp",        Description = "Bản đồ in địa hình khu vực thường xảy ra thiên tai", Unit = "tờ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 94,  CategoryId = 99, Name = "Còi báo động khẩn cấp",           Description = "Còi thổi báo động và kêu gọi cứu hộ khẩn cấp", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 95,  CategoryId = 99, Name = "Kính bảo hộ lao động",            Description = "Kính bảo hộ chống bụi và mảnh vỡ khi làm việc", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 96,  CategoryId = 99, Name = "Ba lô khẩn cấp",                  Description = "Ba lô chứa đồ dùng thiết yếu cho tình huống khẩn cấp", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 97,  CategoryId = 99, Name = "Sổ tay và bút ghi chép",          Description = "Bộ sổ tay và bút bi dùng ghi chép thông tin hiện trường", Unit = "bộ",    ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 98,  CategoryId = 99, Name = "Bộ đèn pin đội đầu",              Description = "Đèn pin LED đội đầu rọi sáng rảnh tay", Unit = "bộ",    ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 99,  CategoryId = 99, Name = "Áo phản quang an toàn",           Description = "Áo ghi lê phản quang tăng nhận diện trong đêm", Unit = "chiếc", ItemType = ItemType.Reusable.ToString(),   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 100, CategoryId = 99, Name = "Pháo sáng khẩn cấp",              Description = "Pháo sáng phát tín hiệu cầu cứu khẩn cấp", Unit = "chiếc", ItemType = ItemType.Consumable.ToString(), CreatedAt = now, UpdatedAt = now }
        };

        foreach (var item in items)
        {
            item.ImageUrl = GetReliefItemImageUrl(item.Id);
        }

        modelBuilder.Entity<ReliefItem>().HasData(items);
    }

    private static string? GetReliefItemImageUrl(int id)
    {
        return id switch
        {
            1 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/001-mi-tom_n1u4fq.jpg",
            2 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/002-nuoc-tinh-khiet_xlky5f.png",
            3 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/003-thuoc-ha-sot-paracetamol-500mg_yaeovi.jpg",
            4 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774866312/004-ao-phao-cuu-sinh_ozit6b.jpg",
            5 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/005-bang-ve-sinh_yhudge.png",
            6 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/006-chan-am-giu-nhiet_ivibn8.png",
            7 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/007-sua-bot-tre-em_vzydxc.png",
            8 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/008-luong-kho_xhokm0.png",
            9 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/009-dau-gio_rbndq6.jpg",
            10 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/010-sat-vitamin-tong-hop_rtdjgu.png",
            11 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/011-gao-say-kho_urtmri.jpg",
            12 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/012-chao-an-lien_rgwjcq.jpg",
            13 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/013-banh-mi-kho_xe7rew.jpg",
            14 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/014-muoi-tinh_odzyix.png",
            15 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/015-duong-cat-trang_vfhuvv.png",
            16 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/016-dau-an-thuc-vat_l41nwp.jpg",
            17 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/017-thit-hop-dong-goi_xrvcnj.png",
            18 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/018-nuoc-loc-binh-20l_xyk8mp.png",
            19 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/019-vien-loc-nuoc-khan-cap_jrezrb.jpg",
            20 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/020-nuoc-dong-thung-24-chai_ktfzck.jpg",
            21 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/021-binh-loc-nuoc-da-chien_gy22py.jpg",
            22 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/022-nuoc-khoang-thien-nhien-500ml_fcjxnc.jpg",
            23 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/023-can-dung-nuoc-10l_bkqljt.png",
            24 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/024-tui-dung-nuoc-linh-hoat_zpizku.jpg",
            25 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/025-nuoc-dua-dong-hop_t0ytn2.png",
            26 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/026-bot-bu-dien-giai-ors_s47y7a.jpg",
            27 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/027-bang-gac-y-te-vo-khuan_c2mkww.jpg",
            28 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/028-bong-gon-y-te_jb2euw.png",
            29 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/029-thuoc-khang-sinh-amoxicillin_hes4wt.png",
            30 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/030-dung-dich-sat-khuan-betadine_zhbkce.jpg",
            31 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/031-nhiet-ke-dien-tu_wxgjdw.png",
            32 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/032-khau-trang-y-te-3-lop_darfut.jpg",
            33 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/033-bo-so-cuu-co-ban_ws83xn.png",
            34 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/034-xa-phong-diet-khuan_g09ho0.png",
            35 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/035-nuoc-rua-tay-kho_bxhmvl.jpg",
            36 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/036-khan-uot-khang-khuan_wwoh14.png",
            37 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/037-kem-danh-rang_s2ibzl.jpg",
            38 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/038-ban-chai-danh-rang_vd42ax.png",
            39 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/039-dau-goi-dau_o9njdq.jpg",
            40 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/040-khan-bong-tam_o94plx.png",
            41 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/041-giay-ve-sinh_c3fryk.jpg",
            42 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/042-ta-dung-mot-lan_yixozm.jpg",
            43 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/043-ao-mua-nguoi-lon_fc7kry.jpg",
            44 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/044-ung-cao-su-chong-lu_lz9qbw.jpg",
            45 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/045-bo-quan-ao-tre-em_n4agu9.jpg",
            46 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/046-ao-am-nguoi-lon_ma6thc.jpg",
            47 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/047-bo-quan-ao-nguoi-lon_umzueu.png",
            48 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/048-bo-quan-ao-nguoi-cao-tuoi_por2xe.jpg",
            49 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/049-gang-tay-giu-am_k56rfm.jpg",
            50 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/050-tat-len-giu-am_ov0jjd.jpg",
            51 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/051-mu-len_wzipsi.jpg",
            52 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865757/052-ao-mua-tre-em_b0mocf.jpg",
            53 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/053-leu-bat-cuu-tro-4-nguoi_qj8w9i.png",
            54 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/054-tam-bat-che-mua-da-nang_xvvydi.jpg",
            55 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/055-tui-ngu-giu-nhiet_mnhbww.jpg",
            56 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/056-dem-hoi-da-chien_ns7izi.jpg",
            57 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/057-man-chong-con-trung_iip3fn.jpg",
            58 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/058-bo-coc-va-day-leu_ywukij.jpg",
            59 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/059-tam-bat-chong-tham_ensdzn.jpg",
            60 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/060-day-buoc-da-nang_mpzo8n.jpg",
            61 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/061-den-led-da-chien_hcylgj.jpg",
            62 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/062-nen-khan-cap_fwzazj.png",
            63 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/063-bua-dong-dinh_ulqde0.jpg",
            64 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/064-dinh-cac-loai_k7fsm9.jpg",
            65 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/065-cua-tay-da-nang_jopzf5.jpg",
            66 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/066-tua-vit-2-dau_tzzrzx.jpg",
            67 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/067-kim-cat-day_tiq6jt.jpg",
            68 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/068-bang-keo-chong-tham_bbctyd.jpg",
            69 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/069-dao-da-nang-da-chien_n68ore.jpg",
            70 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/070-xeng-tay_ktfrdj.jpg",
            71 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/071-bao-cat-chong-lu_cvey61.jpg",
            72 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/072-bo-dung-cu-sua-chua-dien-co-ban_k2peyh.jpg",
            73 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/073-xuong-cao-su-cuu-ho_t3gcxt.jpg",
            74 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/074-day-thung-cuu-sinh-30m_nepsc3.png",
            75 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/075-phao-tron-cuu-sinh_fosz4i.jpg",
            76 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/076-may-bom-nuoc-di-dong_npf0tr.jpg",
            77 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/077-bo-dam-lien-lac-da-chien_kwbfsm.jpg",
            78 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/078-den-tin-hieu-khan-cap_o3frpt.jpg",
            79 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/078-den-tin-hieu-khan-cap_yp3mui.jpg",
            80 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/080-cang-khieng-thuong_xszlmj.jpg",
            81 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/081-mu-bao-hiem-cuu-ho_qetnbw.jpg",
            82 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/082-than-to-ong_m7sdry.jpg",
            83 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/083-may-suoi-dien-mini_hy0wg4.png",
            84 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/084-tui-suoi-am-tay-dung-mot-lan_sadxtb.jpg",
            85 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/085-bo-quan-ao-nhiet_wxsmmj.jpg",
            86 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/086-am-dun-nuoc-du-lich_vbh2ap.jpg",
            87 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/087-bep-gas-du-lich-mini_zeyjrk.jpg",
            88 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/088-binh-gas-mini-da-chien_yeapzn.jpg",
            89 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865734/089-chan-dien-suoi_kvul8o.jpg",
            90 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/090-tam-suoi-am-buc-xa_tysxho.png",
            91 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/091-pin-du-phong-10000mah_gczx45.jpg",
            92 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/092-cap-sac-da-nang_knsvuy.jpg",
            93 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/093-ban-do-dia-hinh-khan-cap_pm5zkt.jpg",
            94 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/094-coi-bao-dong-khan-cap_ukvhal.png",
            95 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/095-kinh-bao-ho-lao-dong_wl8n1f.jpg",
            96 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/096-ba-lo-khan-cap_jn7icq.jpg",
            97 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/097-so-tay-va-but-ghi-chep_h9lums.jpg",
            98 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/098-bo-den-pin-doi-dau_ucnidx.jpg",
            99 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/099-ao-phan-quang-an-toan_trpgia.jpg",
            100 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/100-phao-sang-khan-cap_t0nxwi.jpg",
            101 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/101-xe-tai-cuu-tro-2-5-tan_ifxbqk.jpg",
            102 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/102-xe-cuu-thuong_zqevrt.png",
            103 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/103-xe-ban-tai-4x4_wrs2t4.png",
            104 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/104-xe-may-dia-hinh_xphh0x.png",
            105 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/105-ca-no-cuu-ho_lzudkx.jpg",
            106 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/106-xe-cho-hang-nhe-1-tan_rrmaie.png",
            107 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/107-xe-tai-dong-lanh-3-5-tan_ttxps8.jpg",
            108 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/108-xe-khach-16-cho_h3tjcc.jpg",
            109 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/109-xe-cau-di-dong_xcphgy.jpg",
            110 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/110-xe-chuyen-dung-phong-chay_xoomtb.jpg",
            _ => null
        };
    }

    private static void SeedItemModelTargetGroups(ModelBuilder modelBuilder)
    {
        // TargetGroup IDs matching Domain enum integer values:
        // 1=Children, 2=Elderly, 3=Pregnant, 4=Adult, 5=Rescuer
        //
        // Multi-target-group logic:
        //   - Basic food/water/blankets → all civilian groups + Rescuer (field use)
        //   - Paediatric/elderly/maternal items → specific vulnerable groups only
        //   - Medical consumables → civilian groups + Rescuer (field first-aid)
        //   - Hygiene consumables → relevant civilian groups + Rescuer (field hygiene)
        //   - Raincoats / boots → Adult + Rescuer (rescuers wear them too)
        //   - Reusable rescue equipment → Rescuer only
        //   - Vehicles → Rescuer only

        modelBuilder.Entity("item_model_target_groups").HasData(
            // ── Category 1: Thực phẩm ─────────────────────────────────────────
            // Mì tôm – dễ nấu, rescuer dùng trong hiện trường
            new { item_model_id = 1,  target_group_id = 4 }, // Adult
            new { item_model_id = 1,  target_group_id = 5 }, // Rescuer
            // Sữa bột trẻ em – chỉ dành cho trẻ
            new { item_model_id = 7,  target_group_id = 1 }, // Children
            // Lương khô – khẩu phần dã chiến cho rescuer và adult
            new { item_model_id = 8,  target_group_id = 4 }, // Adult
            new { item_model_id = 8,  target_group_id = 5 }, // Rescuer
            // Gạo sấy khô – thực phẩm cơ bản, dùng cho nhiều nhóm
            new { item_model_id = 11, target_group_id = 4 }, // Adult
            new { item_model_id = 11, target_group_id = 2 }, // Elderly
            new { item_model_id = 11, target_group_id = 3 }, // Pregnant
            new { item_model_id = 11, target_group_id = 5 }, // Rescuer
            // Cháo ăn liền – mềm, dễ tiêu, phù hợp người già / bà bầu / trẻ em
            new { item_model_id = 12, target_group_id = 2 }, // Elderly
            new { item_model_id = 12, target_group_id = 1 }, // Children
            new { item_model_id = 12, target_group_id = 3 }, // Pregnant
            // Bánh mì khô – tiện lợi ngoài hiện trường
            new { item_model_id = 13, target_group_id = 4 }, // Adult
            new { item_model_id = 13, target_group_id = 5 }, // Rescuer
            // Muối tinh – gia vị cơ bản
            new { item_model_id = 14, target_group_id = 4 }, // Adult
            // Đường cát trắng – gia vị cơ bản
            new { item_model_id = 15, target_group_id = 4 }, // Adult
            // Dầu ăn thực vật – gia vị cơ bản
            new { item_model_id = 16, target_group_id = 4 }, // Adult
            // Thịt hộp đóng gói – nguồn protein, rescuer dùng ngoài hiện trường
            new { item_model_id = 17, target_group_id = 4 }, // Adult
            new { item_model_id = 17, target_group_id = 5 }, // Rescuer

            // ── Category 2: Nước uống ─────────────────────────────────────────
            // Nước tinh khiết – thiết yếu cho tất cả
            new { item_model_id = 2,  target_group_id = 4 }, // Adult
            new { item_model_id = 2,  target_group_id = 1 }, // Children
            new { item_model_id = 2,  target_group_id = 2 }, // Elderly
            new { item_model_id = 2,  target_group_id = 3 }, // Pregnant
            new { item_model_id = 2,  target_group_id = 5 }, // Rescuer
            // Nước lọc bình 20L – dùng chung cho cộng đồng
            new { item_model_id = 18, target_group_id = 4 }, // Adult
            // Viên lọc nước khẩn cấp – rescuer lọc nước tại hiện trường
            new { item_model_id = 19, target_group_id = 4 }, // Adult
            new { item_model_id = 19, target_group_id = 5 }, // Rescuer
            // Nước đóng thùng 24 chai
            new { item_model_id = 20, target_group_id = 4 }, // Adult
            // Nước khoáng thiên nhiên 500ml
            new { item_model_id = 22, target_group_id = 4 }, // Adult
            // Nước dừa đóng hộp
            new { item_model_id = 25, target_group_id = 4 }, // Adult
            // Bột bù điện giải ORS – quan trọng cho mọi nhóm khi mất nước
            new { item_model_id = 26, target_group_id = 4 }, // Adult
            new { item_model_id = 26, target_group_id = 1 }, // Children
            new { item_model_id = 26, target_group_id = 2 }, // Elderly
            new { item_model_id = 26, target_group_id = 3 }, // Pregnant
            new { item_model_id = 26, target_group_id = 5 }, // Rescuer

            // ── Category 3: Y tế ──────────────────────────────────────────────
            // Thuốc hạ sốt Paracetamol 500mg – dùng rộng rãi, kể cả rescuer
            new { item_model_id = 3,  target_group_id = 4 }, // Adult
            new { item_model_id = 3,  target_group_id = 2 }, // Elderly
            new { item_model_id = 3,  target_group_id = 5 }, // Rescuer
            // Dầu gió – giảm đau, chống lạnh; người lớn tuổi và adult đều dùng
            new { item_model_id = 9,  target_group_id = 2 }, // Elderly
            new { item_model_id = 9,  target_group_id = 4 }, // Adult
            // Sắt & Vitamin tổng hợp – chủ yếu cho bà bầu
            new { item_model_id = 10, target_group_id = 3 }, // Pregnant
            // Băng gạc y tế vô khuẩn – sơ cứu nạn nhân và cứu hộ viên bị thương
            new { item_model_id = 27, target_group_id = 4 }, // Adult
            new { item_model_id = 27, target_group_id = 5 }, // Rescuer
            // Bông gòn y tế – sơ cứu cho cả nạn nhân lẫn rescuer
            new { item_model_id = 28, target_group_id = 4 }, // Adult
            new { item_model_id = 28, target_group_id = 5 }, // Rescuer
            // Thuốc kháng sinh Amoxicillin – kê đơn, dành cho adult
            new { item_model_id = 29, target_group_id = 4 }, // Adult
            // Dung dịch sát khuẩn Betadine – rescuer cần sát khuẩn vết thương ngoài hiện trường
            new { item_model_id = 30, target_group_id = 4 }, // Adult
            new { item_model_id = 30, target_group_id = 5 }, // Rescuer
            // Khẩu trang y tế 3 lớp – bảo vệ cho tất cả các nhóm
            new { item_model_id = 32, target_group_id = 4 }, // Adult
            new { item_model_id = 32, target_group_id = 1 }, // Children
            new { item_model_id = 32, target_group_id = 2 }, // Elderly
            new { item_model_id = 32, target_group_id = 3 }, // Pregnant
            new { item_model_id = 32, target_group_id = 5 }, // Rescuer
            // Bộ sơ cứu cơ bản – rescuer mang theo trong nhiệm vụ
            new { item_model_id = 33, target_group_id = 4 }, // Adult
            new { item_model_id = 33, target_group_id = 5 }, // Rescuer

            // ── Category 4: Vệ sinh cá nhân ───────────────────────────────────
            // Băng vệ sinh – phụ nữ adult và bà bầu
            new { item_model_id = 5,  target_group_id = 4 }, // Adult
            new { item_model_id = 5,  target_group_id = 3 }, // Pregnant
            // Xà phòng diệt khuẩn
            new { item_model_id = 34, target_group_id = 4 }, // Adult
            // Nước rửa tay khô – rescuer cần giữ vệ sinh ngoài hiện trường
            new { item_model_id = 35, target_group_id = 4 }, // Adult
            new { item_model_id = 35, target_group_id = 5 }, // Rescuer
            // Khăn ướt kháng khuẩn – tiện cho trẻ em và rescuer
            new { item_model_id = 36, target_group_id = 4 }, // Adult
            new { item_model_id = 36, target_group_id = 1 }, // Children
            new { item_model_id = 36, target_group_id = 5 }, // Rescuer
            // Kem đánh răng
            new { item_model_id = 37, target_group_id = 4 }, // Adult
            // Bàn chải đánh răng
            new { item_model_id = 38, target_group_id = 4 }, // Adult
            // Dầu gội đầu
            new { item_model_id = 39, target_group_id = 4 }, // Adult
            // Khăn bông tắm
            new { item_model_id = 40, target_group_id = 4 }, // Adult
            // Giấy vệ sinh
            new { item_model_id = 41, target_group_id = 4 }, // Adult
            // Tã dùng một lần – trẻ em là chủ yếu
            new { item_model_id = 42, target_group_id = 1 }, // Children

            // ── Category 5: Quần áo ───────────────────────────────────────────
            // Áo mưa người lớn – rescuer mặc khi tác nghiệp
            new { item_model_id = 43, target_group_id = 4 }, // Adult
            new { item_model_id = 43, target_group_id = 5 }, // Rescuer
            // Ủng cao su chống lũ – rescuer di chuyển vùng ngập
            new { item_model_id = 44, target_group_id = 4 }, // Adult
            new { item_model_id = 44, target_group_id = 5 }, // Rescuer
            // Bộ quần áo trẻ em
            new { item_model_id = 45, target_group_id = 1 }, // Children
            // Áo ấm người lớn – bà bầu và người cao tuổi cũng cần áo ấm
            new { item_model_id = 46, target_group_id = 4 }, // Adult
            new { item_model_id = 46, target_group_id = 2 }, // Elderly
            new { item_model_id = 46, target_group_id = 3 }, // Pregnant
            // Bộ quần áo người lớn – cũng phù hợp người già
            new { item_model_id = 47, target_group_id = 4 }, // Adult
            new { item_model_id = 47, target_group_id = 2 }, // Elderly
            // Bộ quần áo người cao tuổi
            new { item_model_id = 48, target_group_id = 2 }, // Elderly
            // Găng tay giữ ấm
            new { item_model_id = 49, target_group_id = 4 }, // Adult
            // Tất len giữ ấm
            new { item_model_id = 50, target_group_id = 4 }, // Adult
            // Mũ len
            new { item_model_id = 51, target_group_id = 4 }, // Adult
            // Áo mưa trẻ em
            new { item_model_id = 52, target_group_id = 1 }, // Children

            // ── Category 6: Nơi trú ẩn ───────────────────────────────────────
            // Lều bạt cứu trợ 4 người – cấp cho nạn nhân, rescuer cũng dựng trại
            new { item_model_id = 53, target_group_id = 4 }, // Adult
            new { item_model_id = 53, target_group_id = 5 }, // Rescuer
            // Tấm bạt che mưa đa năng
            new { item_model_id = 54, target_group_id = 4 }, // Adult
            // Túi ngủ giữ nhiệt
            new { item_model_id = 55, target_group_id = 4 }, // Adult
            // Đệm hơi dã chiến
            new { item_model_id = 56, target_group_id = 4 }, // Adult
            // Màn chống côn trùng
            new { item_model_id = 57, target_group_id = 4 }, // Adult
            // Bộ cọc và dây lều – Reusable, dùng bởi rescuer
            new { item_model_id = 58, target_group_id = 5 }, // Rescuer
            // Tấm bạt chống thấm – rescuer phủ thiết bị ngoài hiện trường
            new { item_model_id = 59, target_group_id = 4 }, // Adult
            new { item_model_id = 59, target_group_id = 5 }, // Rescuer
            // Dây buộc đa năng – Reusable
            new { item_model_id = 60, target_group_id = 5 }, // Rescuer
            // Đèn LED dã chiến – Reusable
            new { item_model_id = 61, target_group_id = 5 }, // Rescuer
            // Nến khẩn cấp – rescuer cũng dùng chiếu sáng tạm thời
            new { item_model_id = 62, target_group_id = 4 }, // Adult
            new { item_model_id = 62, target_group_id = 5 }, // Rescuer

            // ── Category 7: Công cụ sửa chữa ─────────────────────────────────
            // Búa đóng đinh – Reusable
            new { item_model_id = 63, target_group_id = 5 }, // Rescuer
            // Đinh các loại – Consumable, rescuer sửa chữa công trình khẩn cấp
            new { item_model_id = 64, target_group_id = 5 }, // Rescuer
            // Cưa tay đa năng – Reusable
            new { item_model_id = 65, target_group_id = 5 }, // Rescuer
            // Tua vít 2 đầu – Reusable
            new { item_model_id = 66, target_group_id = 5 }, // Rescuer
            // Kìm cắt dây – Reusable
            new { item_model_id = 67, target_group_id = 5 }, // Rescuer
            // Băng keo chống thấm – Consumable, dùng trong hiện trường
            new { item_model_id = 68, target_group_id = 5 }, // Rescuer
            // Dao đa năng dã chiến – Reusable
            new { item_model_id = 69, target_group_id = 5 }, // Rescuer
            // Xẻng tay – Reusable
            new { item_model_id = 70, target_group_id = 5 }, // Rescuer
            // Bao cát chống lũ – Reusable
            new { item_model_id = 71, target_group_id = 5 }, // Rescuer
            // Bộ dụng cụ sửa chữa điện cơ bản – Reusable
            new { item_model_id = 72, target_group_id = 5 }, // Rescuer

            // ── Category 8: Thiết bị cứu hộ (tất cả Reusable, chỉ Rescuer) ──
            new { item_model_id = 4,  target_group_id = 5 }, // Áo phao cứu sinh
            new { item_model_id = 21, target_group_id = 5 }, // Bình lọc nước dã chiến
            new { item_model_id = 23, target_group_id = 5 }, // Can đựng nước 10L
            new { item_model_id = 24, target_group_id = 5 }, // Túi đựng nước linh hoạt
            new { item_model_id = 31, target_group_id = 5 }, // Nhiệt kế điện tử
            new { item_model_id = 73, target_group_id = 5 }, // Xuồng cao su cứu hộ
            new { item_model_id = 74, target_group_id = 5 }, // Dây thừng cứu sinh 30m
            new { item_model_id = 75, target_group_id = 5 }, // Phao tròn cứu sinh
            new { item_model_id = 76, target_group_id = 5 }, // Máy bơm nước di động
            new { item_model_id = 77, target_group_id = 5 }, // Bộ đàm liên lạc dã chiến
            new { item_model_id = 78, target_group_id = 5 }, // Đèn tín hiệu khẩn cấp
            new { item_model_id = 79, target_group_id = 5 }, // Máy phát điện di động
            new { item_model_id = 80, target_group_id = 5 }, // Cáng khiêng thương
            new { item_model_id = 81, target_group_id = 5 }, // Mũ bảo hiểm cứu hộ

            // ── Category 9: Sưởi ấm ──────────────────────────────────────────
            // Chăn ấm giữ nhiệt – thiết yếu cho tất cả nhóm dân sự dễ tổn thương
            new { item_model_id = 6,  target_group_id = 4 }, // Adult
            new { item_model_id = 6,  target_group_id = 1 }, // Children
            new { item_model_id = 6,  target_group_id = 2 }, // Elderly
            new { item_model_id = 6,  target_group_id = 3 }, // Pregnant
            // Than tổ ong
            new { item_model_id = 82, target_group_id = 4 }, // Adult
            // Máy sưởi điện mini
            new { item_model_id = 83, target_group_id = 4 }, // Adult
            // Túi sưởi ấm tay dùng một lần – rescuer giữ ấm khi tác nghiệp đêm
            new { item_model_id = 84, target_group_id = 4 }, // Adult
            new { item_model_id = 84, target_group_id = 5 }, // Rescuer
            // Bộ quần áo nhiệt
            new { item_model_id = 85, target_group_id = 4 }, // Adult
            // Ấm đun nước du lịch
            new { item_model_id = 86, target_group_id = 4 }, // Adult
            // Bếp gas du lịch mini – rescuer nấu ăn ngoài dã chiến
            new { item_model_id = 87, target_group_id = 4 }, // Adult
            new { item_model_id = 87, target_group_id = 5 }, // Rescuer
            // Bình gas mini dã chiến – kèm bếp, rescuer dùng trực tiếp
            new { item_model_id = 88, target_group_id = 4 }, // Adult
            new { item_model_id = 88, target_group_id = 5 }, // Rescuer
            // Chăn điện sưởi
            new { item_model_id = 89, target_group_id = 4 }, // Adult
            // Tấm sưởi ấm bức xạ
            new { item_model_id = 90, target_group_id = 4 }, // Adult

            // ── Category 10: Phương tiện (Reusable, chỉ Rescuer) ─────────────
            new { item_model_id = 101, target_group_id = 5 }, // Xe tải cứu trợ 2.5 tấn
            new { item_model_id = 102, target_group_id = 5 }, // Xe cứu thương
            new { item_model_id = 103, target_group_id = 5 }, // Xe bán tải 4x4
            new { item_model_id = 104, target_group_id = 5 }, // Xe máy địa hình
            new { item_model_id = 105, target_group_id = 5 }, // Ca nô cứu hộ
            new { item_model_id = 106, target_group_id = 5 }, // Xe chở hàng nhẹ 1 tấn
            new { item_model_id = 107, target_group_id = 5 }, // Xe tải đông lạnh 3.5 tấn
            new { item_model_id = 108, target_group_id = 5 }, // Xe khách 16 chỗ
            new { item_model_id = 109, target_group_id = 5 }, // Xe cẩu di động
            new { item_model_id = 110, target_group_id = 5 }, // Xe chuyên dụng phòng cháy

            // ── Category 99: Khác ─────────────────────────────────────────────
            // Pin dự phòng – rescuer cần sạc thiết bị liên lạc
            new { item_model_id = 91,  target_group_id = 4 }, // Adult
            new { item_model_id = 91,  target_group_id = 5 }, // Rescuer
            // Cáp sạc đa năng – rescuer giữ thiết bị hoạt động
            new { item_model_id = 92,  target_group_id = 4 }, // Adult
            new { item_model_id = 92,  target_group_id = 5 }, // Rescuer
            // Bản đồ địa hình khẩn cấp – chủ yếu rescuer
            new { item_model_id = 93,  target_group_id = 5 }, // Rescuer
            // Còi báo động khẩn cấp – phát tín hiệu cầu cứu, rescuer cũng dùng
            new { item_model_id = 94,  target_group_id = 4 }, // Adult
            new { item_model_id = 94,  target_group_id = 5 }, // Rescuer
            // Kính bảo hộ lao động – Reusable, rescuer
            new { item_model_id = 95,  target_group_id = 5 }, // Rescuer
            // Ba lô khẩn cấp – rescuer mang thiết bị vào hiện trường
            new { item_model_id = 96,  target_group_id = 4 }, // Adult
            new { item_model_id = 96,  target_group_id = 5 }, // Rescuer
            // Sổ tay và bút ghi chép
            new { item_model_id = 97,  target_group_id = 4 }, // Adult
            // Bộ đèn pin đội đầu – Reusable
            new { item_model_id = 98,  target_group_id = 5 }, // Rescuer
            // Áo phản quang an toàn – Reusable
            new { item_model_id = 99,  target_group_id = 5 }, // Rescuer
            // Pháo sáng khẩn cấp – rescuer báo hiệu vị trí
            new { item_model_id = 100, target_group_id = 5 }  // Rescuer
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // CurrentUtilization = Σ consumable Quantity + non-vehicle reusable units + vehicle units
        // Tính từ đúng per-category factors (không phải uniform factor):
        // Depot 1 (Huế):      consumable=441760 + nonVehicle=82  + vehicle=40  = 441882  → Capacity 750000 (~59%)
        // Depot 2 (Đà Nẵng):  consumable=624480 + nonVehicle=103 + vehicle=31  = 624614  → Capacity 800000 (~78%)
        // Depot 3 (Hà Tĩnh):  consumable=285890 + nonVehicle=65  + vehicle=24  = 285979  → Capacity 450000 (~64%)
        // Depot 4 (HN/TW):    consumable=817990 + nonVehicle=84  + vehicle=49  = 818123  → Capacity 1000000 (~82%)
        // Depot 5 (Quảng Nam — trống, dùng test tiếp nhận đóng kho): 0
        modelBuilder.Entity<Depot>().HasData(
            new Depot { Id = 1, Name = "Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", Address = "46 Đống Đa, TP. Huế, Thừa Thiên Huế", Location = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 750000,  CurrentUtilization = 441882, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg" },
            new Depot { Id = 2, Name = "Ủy ban MTTQVN TP Đà Nẵng", Address = "270 Trưng Nữ Vương, Hải Châu, Đà Nẵng", Location = new Point(108.22283205420794, 16.080298466000496) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 800000,  CurrentUtilization = 624614, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 3, Name = "Ủy Ban MTTQ Tỉnh Hà Tĩnh", Address = "72 Phan Đình Phùng, TP. Hà Tĩnh, Hà Tĩnh", Location = new Point(105.90102499916586, 18.349622333272194) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 450000,  CurrentUtilization = 285979, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg" },
            new Depot { Id = 4, Name = "Ủy ban MTTQVN Việt Nam", Address = "46 Tràng Thi, Hoàn Kiếm, Hà Nội", Location = new Point(105.842191, 21.027819) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 1000000, CurrentUtilization = 818123, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 5, Name = "Ủy ban MTTQVN Tỉnh Quảng Nam", Address = "72 Hùng Vương, TP. Tam Kỳ, Quảng Nam", Location = new Point(108.47388, 15.57360) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 900000,  CurrentUtilization = 0, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" }
        );
    }

    private static void SeedDepotManagers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotManager>().HasData(
            new DepotManager { Id = 1, DepotId = 1, UserId = SeedConstants.ManagerUserId,   AssignedAt = now },
            new DepotManager { Id = 2, DepotId = 2, UserId = SeedConstants.Manager2UserId,  AssignedAt = now },
            new DepotManager { Id = 3, DepotId = 3, UserId = SeedConstants.Manager3UserId,  AssignedAt = now },
            new DepotManager { Id = 4, DepotId = 4, UserId = SeedConstants.Manager4UserId,  AssignedAt = now },
            new DepotManager { Id = 5, DepotId = 5, UserId = SeedConstants.Manager5UserId,  AssignedAt = now }
        );
    }

    // ── Consumable items → tracked by quantity in depot_supply_inventory ──────
    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // 72 consumable relief item IDs (same order as before — preserves DSI IDs)
        // Index map (0-based): 0:Id1, 1:Id2, 2:Id3, 3:Id5, 4:Id6, 5:Id7, 6:Id8, 7:Id9, 8:Id10,
        //   9:Id11, 10:Id12, 11:Id13, 12:Id14, 13:Id15, 14:Id16, 15:Id17,
        //   16:Id18, 17:Id19, 18:Id20, 19:Id22, 20:Id25, 21:Id26,
        //   22:Id27, 23:Id28, 24:Id29, 25:Id30, 26:Id32, 27:Id33,
        //   28:Id34, 29:Id35, 30:Id36, 31:Id37, 32:Id38, 33:Id39, 34:Id40, 35:Id41, 36:Id42,
        //   37:Id43, 38:Id44, 39:Id45, 40:Id46, 41:Id47, 42:Id48, 43:Id49, 44:Id50, 45:Id51, 46:Id52,
        //   47:Id53, 48:Id54, 49:Id55, 50:Id56, 51:Id57, 52:Id59, 53:Id62,
        //   54:Id64, 55:Id68,
        //   56:Id82, 57:Id83, 58:Id84, 59:Id85, 60:Id86, 61:Id87, 62:Id88, 63:Id89, 64:Id90,
        //   65:Id91, 66:Id92, 67:Id93, 68:Id94, 69:Id96, 70:Id97, 71:Id100
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

        // Category of each item (same order as consumableIds)
        int[] itemCategoryId =
        {
            1, 2, 3, 4, 9, 1, 1, 3, 3, 1, 1, 1, 1, 1, 1, 1,  // idx 0-15
            2, 2, 2, 2, 2, 2,                                   // idx 16-21
            3, 3, 3, 3, 3, 3,                                   // idx 22-27
            4, 4, 4, 4, 4, 4, 4, 4, 4,                         // idx 28-36
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,                      // idx 37-46
            6, 6, 6, 6, 6, 6, 6,                                // idx 47-53
            7, 7,                                                // idx 54-55
            9, 9, 9, 9, 9, 9, 9, 9, 9,                         // idx 56-64
            99, 99, 99, 99, 99, 99, 99                          // idx 65-71
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

        // ── Per-category, per-depot quantity factors ─────────────────────────
        // Depot order: D1-Huế(0), D2-Đà Nẵng(1), D3-Hà Tĩnh(2), D4-HN-TW(3)
        // Low factors (< 0.4) are intentional to create supply shortage test data
        var categoryFactors = new Dictionary<int, double[]>
        {
            [1]  = new[] { 1.2,  0.7,  0.25, 1.5  }, // Food       — D3 LOW ⚠️
            [2]  = new[] { 0.8,  1.3,  0.6,  1.4  }, // Water
            [3]  = new[] { 0.25, 1.4,  0.7,  1.5  }, // Medical    — D1 LOW ⚠️
            [4]  = new[] { 0.9,  1.2,  0.2,  1.3  }, // Hygiene    — D3 LOW ⚠️
            [5]  = new[] { 1.1,  0.3,  1.3,  0.9  }, // Clothing   — D2 LOW ⚠️
            [6]  = new[] { 1.0,  0.6,  1.2,  0.8  }, // Shelter
            [7]  = new[] { 1.0,  0.8,  1.0,  1.2  }, // RepairTools
            [9]  = new[] { 0.35, 0.3,  1.5,  0.8  }, // Heating    — D1+D2 LOW ⚠️
            [99] = new[] { 0.6,  0.8,  0.3,  1.2  }, // Others     — D3 LOW ⚠️
        };

        int[] depotIds = { 1, 2, 3, 4 };

        var list = new List<DepotSupplyInventory>();
        int id = 1;

        for (int d = 0; d < depotIds.Length; d++)
        {
            for (int i = 0; i < consumableIds.Length; i++)
            {
                int catId = itemCategoryId[i];
                double factor = categoryFactors.TryGetValue(catId, out var catFactors)
                    ? catFactors[d]
                    : 1.0;
                int qty = Math.Max(1, (int)(baseQty[i] * factor));
                list.Add(new DepotSupplyInventory
                {
                    Id = id,
                    DepotId = depotIds[d],
                    ItemModelId = consumableIds[i],
                    Quantity = qty,
                    MissionReservedQuantity = qty / 10,
                    TransferReservedQuantity = 0,
                    LastStockedAt = now
                });
                id++;
            }
        }

        // ── Low-stock seed overrides ─────────────────────────────────────────
        // DSI ID formula: id = depotIndex * 72 + itemIndex + 1
        //   D1 (Huế):    IDs  1- 72  | D2 (Đà Nẵng): IDs  73-144
        //   D3 (Hà Tĩnh): IDs 145-216 | D4 (HN-TW):   IDs 217-288
        //
        // Each depot has 3 consumable items intentionally pushed into the fixed warning bands:
        // CRITICAL [0.0, 0.4), MEDIUM [0.4, 0.7), LOW [0.7, 1.0). The rest remain OK (>= 1.0).
        // Value = target remaining available ratio after mission reservation.
        // Ratios are kept away from boundaries so rounding still lands in the intended band.
        var lowStockAvailabilityRatios = new Dictionary<int, decimal>
        {
            // Depot 1 - Hue
            [3]  = 0.20m, // Paracetamol: CRITICAL
            [23] = 0.55m, // Medical gauze: MEDIUM
            [5]  = 0.82m, // Blanket: LOW

            // Depot 2 - Da Nang
            [129] = 0.20m, // Coal briquette: CRITICAL
            [110] = 0.55m, // Adult raincoat: MEDIUM
            [113] = 0.82m, // Adult warm jacket: LOW

            // Depot 3 - Ha Tinh
            [145] = 0.20m, // Instant noodles: CRITICAL
            [154] = 0.55m, // Dried rice: MEDIUM
            [172] = 0.82m, // First aid kit: LOW

            // Depot 4 - HN/TW
            [219] = 0.20m, // Paracetamol: CRITICAL
            [226] = 0.55m, // Dried rice: MEDIUM
            [245] = 0.82m, // Antibacterial soap: LOW
        };
        foreach (var entry in list)
        {
            if (!lowStockAvailabilityRatios.TryGetValue(entry.Id, out var availableRatio))
                continue;

            var quantity = entry.Quantity ?? 1;
            var reserved = (int)Math.Floor(quantity * (1m - availableRatio));
            entry.MissionReservedQuantity = Math.Clamp(reserved, 0, quantity - 1);
        }

        // ── Transfer reservation overrides — khớp với supply requests đã seed ──
        // Request #1 (Depot 1 = Huế là kho nguồn, trạng thái Accepted):
        //   DSI 1 = Depot 1, Item #1  (Mì tôm)        → đặt trữ 6000
        //   DSI 2 = Depot 1, Item #2  (Nước tinh khiết) → đặt trữ 4000
        // (DSI id = depotIndex * 72 + itemIndex + 1; Depot 1 = index 0)
        var transferReservedOverrides = new Dictionary<int, int>
        {
            [1] = 6000,
            [2] = 4000,
        };
        foreach (var entry in list)
        {
            if (transferReservedOverrides.TryGetValue(entry.Id, out var transferReserved))
                entry.TransferReservedQuantity = transferReserved;
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

        // ── Reusable item groups with per-depot unit counts ───────────────
        // Per-depot unit table (D1-Huế, D2-Đà Nẵng, D3-Hà Tĩnh, D4-HN-TW)
        //
        // RescueEquipment (4,21,23,24,31,73,74,75,76,77,78,79,80,81) : 3, 4, 2, 3
        // Shelter reusables (58,60,61)                                : 5, 2, 4, 3  (Shelter-rich for Huế+Hà Tĩnh)
        // RepairTools (63,65,66,67,69,70,71,72)                       : 2, 4, 2, 3
        // Cat99 reusables (95,98,99)                                  : 3, 3, 3, 3
        //
        // Heating reusables: none (all heating is consumable)
        // Vehicle (101-110) : scaled by depot factor ×1.0/×0.8/×0.6/×1.2

        // (itemId, unitsPerDepot[D1,D2,D3,D4])
        var reusableGroups = new (int[] ids, int[] units)[]
        {
            // RescueEquipment — D3 slightly lower
            (new[] { 4, 21, 23, 24, 31, 73, 74, 75, 76, 77, 78, 79, 80, 81 }, new[] { 3, 4, 2, 3 }),
            // Shelter reusables — D1 & D3 high (coastal/flood zones)
            (new[] { 58, 60, 61 }, new[] { 5, 2, 4, 3 }),
            // RepairTools reusables — D2 high (urban center)
            (new[] { 63, 65, 66, 67, 69, 70, 71, 72 }, new[] { 2, 4, 2, 3 }),
            // Category 99 reusables
            (new[] { 95, 98, 99 }, new[] { 3, 3, 3, 3 }),
        };

        // ── Vehicle item IDs and base units per depot ──────────────────────
        // 101 Xe tải 2.5T, 102 Xe cứu thương, 103 Xe bán tải 4×4, 104 Xe máy địa hình,
        // 105 Ca nô, 106 Xe chở hàng 1T, 107 Xe đông lạnh, 108 Xe khách 16 chỗ,
        // 109 Xe cẩu, 110 Xe PCCC
        int[] vehicleIds       = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
        int[] vehicleBaseUnits = {   5,   3,   5,   8,   4,   5,   3,   3,   2,   2 };
        double[] vehicleDepotFactors = { 1.0, 0.8, 0.6, 1.2 };

        int[] depotIds = { 1, 2, 3, 4 };

        var list = new List<DepotReusableItem>();
        int id = 1;

        for (int d = 0; d < depotIds.Length; d++)
        {
            // ── Non-vehicle reusable items ──
            foreach (var (ids, unitCounts) in reusableGroups)
            {
                int units = unitCounts[d];
                foreach (int itemId in ids)
                {
                    for (int u = 1; u <= units; u++)
                    {
                        // First 2/3 units are Good, last 1/3 are Fair
                        string condition = u <= Math.Max(1, units * 2 / 3) ? good : fair;
                        list.Add(new DepotReusableItem
                        {
                            Id = id++,
                            DepotId = depotIds[d],
                            ItemModelId = itemId,
                            SerialNumber = $"D{depotIds[d]}-R{itemId:D3}-{u:D3}",
                            Status = available,
                            Condition = condition,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                }
            }

            // ── Vehicle items: variable units per type, scaled by depot factor ──
            for (int v = 0; v < vehicleIds.Length; v++)
            {
                int units = Math.Max(1, (int)(vehicleBaseUnits[v] * vehicleDepotFactors[d]));
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

        // ── Test seed: 2 áo phao cứu sinh tại Depot 1 (Huế) được nhọn chọn là InUse ──────────────────────
        // Phục vụ test endpoint: POST /operations/missions/5/activities/8/confirm-return
        // Login bằng: manager@resq.vn / Manager@123
        var inUse = ReusableItemStatus.InUse.ToString();
        list.Single(x => x.Id == 1).Status = inUse; // D1-R004-001, Áo phao cứu sinh, Good
        list.Single(x => x.Id == 2).Status = inUse; // D1-R004-002, Áo phao cứu sinh, Good

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

    // ── Depot-to-depot supply requests ────────────────────────────────────────
    private static void SeedDepotSupplyRequests(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyRequest>().HasData(
            // Req 1 (IN PROGRESS — Accepted/Approved): Kho 3 (Hà Tĩnh) xin từ Kho 1 (Huế)
            // Kho nguồn đã chấp nhận, đang chuẩn bị hàng. TransferReserved đã set ở DSI.
            new DepotSupplyRequest
            {
                Id = 1, RequestingDepotId = 3, SourceDepotId = 1,
                Note = "Thiếu lương thực và nước uống cứu trợ khẩn cấp",
                PriorityLevel     = "High",
                SourceStatus      = SourceDepotStatus.Accepted.ToString(),
                RequestingStatus  = RequestingDepotStatus.Approved.ToString(),
                RequestedBy       = SeedConstants.Manager3UserId,
                CreatedAt         = now,
                AutoRejectAt      = now.AddHours(2),
                RespondedAt       = now.AddHours(1)
            },
            // Req 2 (COMPLETED — 2 bên đã xác nhận): Kho 2 (Đà Nẵng) xin từ Kho 1 (Huế)
            new DepotSupplyRequest
            {
                Id = 2, RequestingDepotId = 2, SourceDepotId = 1,
                Note = "Bổ sung thuốc y tế cho kho Đà Nẵng",
                PriorityLevel     = "Medium",
                SourceStatus      = SourceDepotStatus.Completed.ToString(),
                RequestingStatus  = RequestingDepotStatus.Received.ToString(),
                RequestedBy       = SeedConstants.Manager2UserId,
                CreatedAt         = now.AddDays(-20),
                AutoRejectAt      = now.AddDays(-20).AddHours(2),
                RespondedAt       = now.AddDays(-19),
                ShippedAt         = now.AddDays(-18),
                CompletedAt       = now.AddDays(-16)
            },
            // Req 3 (COMPLETED — 2 bên đã xác nhận): Kho 4 (HN) xin từ Kho 2 (Đà Nẵng)
            new DepotSupplyRequest
            {
                Id = 3, RequestingDepotId = 4, SourceDepotId = 2,
                Note = "Bổ sung nước uống cho kho trung ương",
                PriorityLevel     = "Medium",
                SourceStatus      = SourceDepotStatus.Completed.ToString(),
                RequestingStatus  = RequestingDepotStatus.Received.ToString(),
                RequestedBy       = SeedConstants.Manager4UserId,
                CreatedAt         = now.AddDays(-14),
                AutoRejectAt      = now.AddDays(-14).AddHours(2),
                RespondedAt       = now.AddDays(-13),
                ShippedAt         = now.AddDays(-12),
                CompletedAt       = now.AddDays(-10)
            },
            // Req 4 (COMPLETED — 2 bên đã xác nhận): Kho 1 (Huế) xin từ Kho 4 (HN)
            new DepotSupplyRequest
            {
                Id = 4, RequestingDepotId = 1, SourceDepotId = 4,
                Note = "Bổ sung chăn ấm và thiết bị sưởi từ kho trung ương",
                PriorityLevel     = "Low",
                SourceStatus      = SourceDepotStatus.Completed.ToString(),
                RequestingStatus  = RequestingDepotStatus.Received.ToString(),
                RequestedBy       = SeedConstants.ManagerUserId,
                CreatedAt         = now.AddDays(-8),
                AutoRejectAt      = now.AddDays(-8).AddHours(2),
                RespondedAt       = now.AddDays(-7),
                ShippedAt         = now.AddDays(-6),
                CompletedAt       = now.AddDays(-4)
            },
            // Req 5 (REJECTED): Kho 1 (Huế) xin từ Kho 3 (Hà Tĩnh) — bị từ chối
            new DepotSupplyRequest
            {
                Id = 5, RequestingDepotId = 1, SourceDepotId = 3,
                Note = "Cần bổ sung dụng cụ cứu hộ khẩn cấp",
                PriorityLevel     = "High",
                SourceStatus      = SourceDepotStatus.Rejected.ToString(),
                RequestingStatus  = RequestingDepotStatus.Rejected.ToString(),
                RejectedReason    = "Kho Hà Tĩnh không đủ tồn kho để đáp ứng",
                RequestedBy       = SeedConstants.ManagerUserId,
                CreatedAt         = now.AddDays(-30),
                AutoRejectAt      = now.AddDays(-30).AddHours(2),
                RespondedAt       = now.AddDays(-30).AddHours(1)
            },
            // Req 6 (COMPLETED — 2 bên đã xác nhận): Kho 1 (Huế) xin từ Kho 2 (Đà Nẵng)
            new DepotSupplyRequest
            {
                Id = 6, RequestingDepotId = 1, SourceDepotId = 2,
                Note = "Bổ sung thuốc y tế dự phòng cho mùa lũ lụt",
                PriorityLevel     = "Medium",
                SourceStatus      = SourceDepotStatus.Completed.ToString(),
                RequestingStatus  = RequestingDepotStatus.Received.ToString(),
                RequestedBy       = SeedConstants.ManagerUserId,
                CreatedAt         = now.AddDays(-25),
                AutoRejectAt      = now.AddDays(-25).AddHours(2),
                RespondedAt       = now.AddDays(-24),
                ShippedAt         = now.AddDays(-23),
                CompletedAt       = now.AddDays(-21)
            }
        );
    }

    private static void SeedDepotSupplyRequestItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepotSupplyRequestItem>().HasData(
            // Req 1 (IN PROGRESS): mì tôm + nước — đặt trữ tại Kho 1
            new DepotSupplyRequestItem { Id = 1, DepotSupplyRequestId = 1, ItemModelId = 1, Quantity = 6000 },
            new DepotSupplyRequestItem { Id = 2, DepotSupplyRequestId = 1, ItemModelId = 2, Quantity = 4000 },
            // Req 2 (COMPLETED): thuốc Paracetamol
            new DepotSupplyRequestItem { Id = 3, DepotSupplyRequestId = 2, ItemModelId = 3, Quantity = 5000 },
            // Req 3 (COMPLETED): nước tinh khiết
            new DepotSupplyRequestItem { Id = 4, DepotSupplyRequestId = 3, ItemModelId = 2, Quantity = 8000 },
            // Req 4 (COMPLETED): chăn ấm giữ nhiệt
            new DepotSupplyRequestItem { Id = 5, DepotSupplyRequestId = 4, ItemModelId = 6, Quantity = 200 },
            // Req 5 (REJECTED): dụng cụ cứu hộ
            new DepotSupplyRequestItem { Id = 6, DepotSupplyRequestId = 5, ItemModelId = 5, Quantity = 50 },
            new DepotSupplyRequestItem { Id = 7, DepotSupplyRequestId = 5, ItemModelId = 4, Quantity = 100 },
            // Req 6 (COMPLETED): thuốc bổ sung
            new DepotSupplyRequestItem { Id = 8, DepotSupplyRequestId = 6, ItemModelId = 3, Quantity = 3000 }
        );
    }
}

