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

        // Quantity = t?ng v?t ph?m theo danh m?c c?a T?T C? kho
        // Consumable: baseQty × (1.0 + 0.8 + 0.6 + 0.9) = baseQty × 3.3
        // Reusable (phi xe): s? item × 3 units/kho × 4 kho = × 12
        // Vehicle: tính t?ng xe theo depot factor
        modelBuilder.Entity<ItemCategory>().HasData(
            new ItemCategory { Id = 1,  Code = "Food",            Name = "Th?c ph?m",         Quantity = 597300,  Description = "Luong th?c, d? an khô",                   CreatedAt = now },
            new ItemCategory { Id = 2,  Code = "Water",           Name = "Nu?c u?ng",         Quantity = 382800,  Description = "Nu?c s?ch, nu?c dóng chai",             CreatedAt = now },
            new ItemCategory { Id = 3,  Code = "Medical",         Name = "Y t?",              Quantity = 574200,  Description = "Thu?c men, d?ng c? so c?u",          CreatedAt = now },
            new ItemCategory { Id = 4,  Code = "Hygiene",         Name = "V? sinh cá nhân",   Quantity = 242550,  Description = "Khan gi?y, xà phòng, bang v? sinh",    CreatedAt = now },
            new ItemCategory { Id = 5,  Code = "Clothing",        Name = "Qu?n áo",            Quantity = 13860,   Description = "Qu?n áo s?ch, áo mua",                  CreatedAt = now },
            new ItemCategory { Id = 6,  Code = "Shelter",         Name = "Noi trú ?n",         Quantity = 18186,   Description = "L?u b?t, túi ng?",                     CreatedAt = now },
            new ItemCategory { Id = 7,  Code = "RepairTools",     Name = "Công c? s?a ch?a",  Quantity = 23196,   Description = "Búa, dinh, cua",                       CreatedAt = now },
            new ItemCategory { Id = 8,  Code = "RescueEquipment", Name = "Thi?t b? c?u h?",  Quantity = 168,     Description = "Áo phao, xu?ng, dây th?ng",              CreatedAt = now },
            new ItemCategory { Id = 9,  Code = "Heating",         Name = "Su?i ?m",            Quantity = 32340,   Description = "Chan, than, máy su?i",                  CreatedAt = now },
            new ItemCategory { Id = 10, Code = "Vehicle",         Name = "Phuong ti?n",        Quantity = 119,     Description = "Xe c?, phuong ti?n v?n chuy?n c?u tr?", CreatedAt = now },
            new ItemCategory { Id = 99, Code = "Others",          Name = "Khác",               Quantity = 8616,    Description = "Các v?t ph?m khác",                    CreatedAt = now }
        );
    }

    private static void SeedOrganizations(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Organization>().HasData(
            new Organization { Id = 1, Name = "H?i Ch? Th?p Ð? - Th?a Thiên Hu?", Phone = "02343822123", Email = "hue@redcross.org.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 2, Name = "?y ban MTTQ Vi?t Nam - Qu?ng Bình", Phone = "02323812345", Email = "mttq@quangbinh.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 3, Name = "Qu? T?m Lòng Vàng - Ðà N?ng", Phone = "02363567890", Email = "contact@tamlongvang-dn.org", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 4, Name = "T?nh Ðoàn Qu?ng Tr?", Phone = "02333852111", Email = "tinhdoan@quangtri.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 5, Name = "H?i Liên hi?p Ph? n? - Hà Tinh", Phone = "02393855222", Email = "phunu@hatinh.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 6, Name = "Nhóm Thi?n Nguy?n Ð?ng Xanh - Qu?ng Nam", Phone = "0905123456", Email = "dongxanh@thiennguyen.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 7, Name = "H?i Ch? Th?p Ð? - Qu?ng Ngãi", Phone = "02553822777", Email = "quangngai@redcross.org.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 8, Name = "Ban Ch? huy PCTT & TKCN Mi?n Trung", Phone = "02363822999", Email = "pctt@mientrung.gov.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 9, Name = "Câu l?c b? Tình Ngu?i - Phú Yên", Phone = "0988765432", Email = "tinhnguoi@phuyen.vn", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Organization { Id = 10, Name = "Qu? B?o tr? Tr? em Mi?n Trung", Phone = "02343811811", Email = "treem@baotromientrung.vn", IsActive = true, CreatedAt = now, UpdatedAt = now }
        );
    }

    private static void SeedReliefItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var items = new[]
        {
            // -- Category 1: Th?c ph?m (Food) - 10 items ----------------------
            new ReliefItem { Id = 1,  CategoryId = 1, Name = "Mì tôm",                        Description = "Mì an li?n dóng gói dùng c?u tr? kh?n c?p", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.8m,    WeightPerUnit = 0.075m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 7,  CategoryId = 1, Name = "S?a b?t tr? em",                Description = "S?a b?t dinh du?ng dành cho tr? em du?i 6 tu?i", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.5m,    WeightPerUnit = 0.4m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 8,  CategoryId = 1, Name = "Luong khô",                     Description = "Luong khô nang lu?ng cao, b?o qu?n lâu dài", Unit = "thanh", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.15m,   WeightPerUnit = 0.06m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 11, CategoryId = 1, Name = "G?o s?y khô",                   Description = "G?o s?y khô an li?n, ch? c?n thêm nu?c nóng", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.6m,    WeightPerUnit = 0.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 12, CategoryId = 1, Name = "Cháo an li?n",                  Description = "Cháo an li?n dóng gói, d? tiêu hóa cho m?i l?a tu?i", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.4m,    WeightPerUnit = 0.065m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 13, CategoryId = 1, Name = "Bánh mì khô",                   Description = "Bánh mì khô b?o qu?n lâu, ti?n l?i khi c?u tr?", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.8m,    WeightPerUnit = 0.15m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 14, CategoryId = 1, Name = "Mu?i tinh",                     Description = "Mu?i tinh tiêu chu?n dùng ch? bi?n th?c ph?m", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.2m,    WeightPerUnit = 0.25m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 15, CategoryId = 1, Name = "Ðu?ng cát tr?ng",               Description = "Ðu?ng cát tr?ng tinh luy?n dùng pha ch? và n?u an", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.35m,   WeightPerUnit = 0.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 16, CategoryId = 1, Name = "D?u an th?c v?t",               Description = "D?u an th?c v?t dóng chai dùng ch? bi?n th?c ph?m", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 1.2m,    WeightPerUnit = 1.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 17, CategoryId = 1, Name = "Th?t h?p dóng gói",             Description = "Th?t h?p dóng gói b?o qu?n lâu, giàu dinh du?ng", Unit = "h?p",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.5m,    WeightPerUnit = 0.35m,   CreatedAt = now, UpdatedAt = now },

            // -- Category 2: Nu?c u?ng (Water) - 7 items (tiêu hao, phát cho n?n nhân) --
            new ReliefItem { Id = 2,  CategoryId = 2, Name = "Nu?c tinh khi?t",               Description = "Nu?c u?ng dóng chai 500ml ph?c v? c?p phát", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.6m,    WeightPerUnit = 0.52m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 18, CategoryId = 2, Name = "Nu?c l?c bình 20L",             Description = "Bình nu?c l?c 20 lít ph?c v? sinh ho?t t?p th?", Unit = "bình",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 22.0m,   WeightPerUnit = 20.5m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 19, CategoryId = 2, Name = "Viên l?c nu?c kh?n c?p",        Description = "Viên l?c nu?c c?m tay, x? lý nu?c b?n thành nu?c u?ng", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.005m,  WeightPerUnit = 0.004m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 20, CategoryId = 2, Name = "Nu?c dóng thùng 24 chai",       Description = "Thùng 24 chai nu?c u?ng 500ml ti?n phân ph?i", Unit = "thùng", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 16.0m,   WeightPerUnit = 13.0m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 22, CategoryId = 2, Name = "Nu?c khoáng thiên nhiên 500ml", Description = "Nu?c khoáng thiên nhiên dóng chai 500ml", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.6m,    WeightPerUnit = 0.53m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 25, CategoryId = 2, Name = "Nu?c d?a dóng h?p",             Description = "Nu?c d?a tuoi dóng h?p b? sung di?n gi?i", Unit = "h?p",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.4m,    WeightPerUnit = 0.35m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 26, CategoryId = 2, Name = "B?t bù di?n gi?i ORS",          Description = "B?t pha bù nu?c và di?n gi?i cho ngu?i m?t nu?c", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.05m,   WeightPerUnit = 0.025m,  CreatedAt = now, UpdatedAt = now },

            // -- Category 3: Y t? (Medical) - 9 items (tiêu hao, c?p phát cho n?n nhân) --
            new ReliefItem { Id = 3,  CategoryId = 3, Name = "Thu?c h? s?t Paracetamol 500mg", Description = "Thu?c h? s?t gi?m dau co b?n cho ngu?i l?n", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.005m,  WeightPerUnit = 0.002m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 9,  CategoryId = 3, Name = "D?u gió",                         Description = "D?u gió xanh dùng xoa bóp gi?m dau, ch?ng c?m", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.04m,   WeightPerUnit = 0.035m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 10, CategoryId = 3, Name = "S?t & Vitamin t?ng h?p",          Description = "Viên u?ng b? sung s?t và vitamin t?ng h?p", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.005m,  WeightPerUnit = 0.002m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 27, CategoryId = 3, Name = "Bang g?c y t? vô khu?n",          Description = "Bang g?c vô khu?n dùng bang bó v?t thuong", Unit = "cu?n",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.15m,   WeightPerUnit = 0.05m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 28, CategoryId = 3, Name = "Bông gòn y t?",                   Description = "Bông gòn y t? vô khu?n dùng v? sinh và so c?u", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.4m,    WeightPerUnit = 0.05m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 29, CategoryId = 3, Name = "Thu?c kháng sinh Amoxicillin",    Description = "Thu?c kháng sinh ph? r?ng di?u tr? nhi?m khu?n", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.005m,  WeightPerUnit = 0.002m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 30, CategoryId = 3, Name = "Dung d?ch sát khu?n Betadine",    Description = "Dung d?ch sát khu?n Povidone-Iodine r?a v?t thuong", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.15m,   WeightPerUnit = 0.12m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 32, CategoryId = 3, Name = "Kh?u trang y t? 3 l?p",           Description = "Kh?u trang y t? dùng m?t l?n, dóng gói vô khu?n", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.04m,   WeightPerUnit = 0.005m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 33, CategoryId = 3, Name = "B? so c?u co b?n",                Description = "B? so c?u g?m bang, g?c, kéo, k?p và thu?c co b?n", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 3.0m,    WeightPerUnit = 1.5m,    CreatedAt = now, UpdatedAt = now },

            // -- Category 4: V? sinh cá nhân (Hygiene) - 10 items -------------
            new ReliefItem { Id = 5,  CategoryId = 4, Name = "Bang v? sinh",              Description = "Bang v? sinh ph? n? dùng m?t l?n, dóng gói riêng", Unit = "mi?ng", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.06m,   WeightPerUnit = 0.015m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 34, CategoryId = 4, Name = "Xà phòng di?t khu?n",      Description = "Xà phòng c?c di?t khu?n dùng v? sinh cá nhân", Unit = "bánh",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.12m,   WeightPerUnit = 0.1m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 35, CategoryId = 4, Name = "Nu?c r?a tay khô",          Description = "Gel r?a tay khô di?t khu?n nhanh, không c?n nu?c", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.3m,    WeightPerUnit = 0.28m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 36, CategoryId = 4, Name = "Khan u?t kháng khu?n",      Description = "Khan u?t kháng khu?n ti?n d?ng, dóng gói 10 t?", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.25m,   WeightPerUnit = 0.1m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 37, CategoryId = 4, Name = "Kem dánh rang",             Description = "Kem dánh rang kích thu?c nh? g?n phù h?p c?u tr?", Unit = "tuýp",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.15m,   WeightPerUnit = 0.12m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 38, CategoryId = 4, Name = "Bàn ch?i dánh rang",        Description = "Bàn ch?i dánh rang dùng m?t l?n, dóng gói riêng", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.06m,   WeightPerUnit = 0.02m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 39, CategoryId = 4, Name = "D?u g?i d?u",               Description = "D?u g?i d?u gói nh? ti?n l?i cho c?u tr?", Unit = "chai",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.25m,   WeightPerUnit = 0.22m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 40, CategoryId = 4, Name = "Khan bông t?m",             Description = "Khan bông t?m c? trung dùng v? sinh cá nhân", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 2.5m,    WeightPerUnit = 0.35m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 41, CategoryId = 4, Name = "Gi?y v? sinh",              Description = "Gi?y v? sinh cu?n nh? tiêu chu?n", Unit = "cu?n",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 1.2m,    WeightPerUnit = 0.1m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 42, CategoryId = 4, Name = "Tã dùng m?t l?n",           Description = "Tã gi?y dùng m?t l?n cho tr? em ho?c ngu?i già", Unit = "mi?ng", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.5m,    WeightPerUnit = 0.06m,   CreatedAt = now, UpdatedAt = now },

            // -- Category 5: Qu?n áo (Clothing) - 10 items --------------------
            new ReliefItem { Id = 43, CategoryId = 5, Name = "Áo mua ngu?i l?n",          Description = "Áo mua nh?a dùng m?t l?n cho ngu?i l?n", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 1.5m,    WeightPerUnit = 0.25m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 44, CategoryId = 5, Name = "?ng cao su ch?ng lu",       Description = "?ng cao su ch?ng nu?c dùng di l?i trong vùng ng?p", Unit = "dôi",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 6.0m,    WeightPerUnit = 1.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 45, CategoryId = 5, Name = "B? qu?n áo tr? em",         Description = "B? qu?n áo s?ch kích thu?c tr? em 3–12 tu?i", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 2.0m,    WeightPerUnit = 0.3m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 46, CategoryId = 5, Name = "Áo ?m ngu?i l?n",           Description = "Áo khoác gi? ?m dùng trong th?i ti?t l?nh", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 4.0m,    WeightPerUnit = 0.7m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 47, CategoryId = 5, Name = "B? qu?n áo ngu?i l?n",      Description = "B? qu?n áo s?ch kích thu?c ngu?i l?n", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 3.5m,    WeightPerUnit = 0.6m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 48, CategoryId = 5, Name = "B? qu?n áo ngu?i cao tu?i", Description = "B? qu?n áo tho?i mái phù h?p ngu?i cao tu?i", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 3.5m,    WeightPerUnit = 0.6m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 49, CategoryId = 5, Name = "Gang tay gi? ?m",           Description = "Gang tay len gi? ?m trong th?i ti?t l?nh", Unit = "dôi",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.3m,    WeightPerUnit = 0.08m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 50, CategoryId = 5, Name = "T?t len gi? ?m",            Description = "T?t len dày gi? ?m chân trong mùa l?nh", Unit = "dôi",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.2m,    WeightPerUnit = 0.06m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 51, CategoryId = 5, Name = "Mu len",                    Description = "Mu len gi? ?m d?u trong th?i ti?t l?nh", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.4m,    WeightPerUnit = 0.08m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 52, CategoryId = 5, Name = "Áo mua tr? em",             Description = "Áo mua nh?a dùng m?t l?n cho tr? em", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 1.0m,    WeightPerUnit = 0.18m,   CreatedAt = now, UpdatedAt = now },

            // -- Category 6: Noi trú ?n (Shelter) - 10 items -----------------
            // Tiêu hao: c?p phát cho n?n nhân trú ?n (không b?t bu?c hoàn tr?)
            new ReliefItem { Id = 53, CategoryId = 6, Name = "L?u b?t c?u tr? 4 ngu?i",   Description = "L?u b?t dã chi?n s?c ch?a 4 ngu?i, ch?ng nu?c", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 30.0m,   WeightPerUnit = 8.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 54, CategoryId = 6, Name = "T?m b?t che mua da nang",   Description = "T?m b?t PE ch?ng nu?c da nang dùng che mua n?ng", Unit = "t?m",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 5.0m,    WeightPerUnit = 1.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 55, CategoryId = 6, Name = "Túi ng? gi? nhi?t",         Description = "Túi ng? cách nhi?t dùng trong th?i ti?t l?nh", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 10.0m,   WeightPerUnit = 1.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 56, CategoryId = 6, Name = "Ð?m hoi dã chi?n",          Description = "Ð?m hoi g?p g?n dùng ng? dã chi?n", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 8.0m,    WeightPerUnit = 2.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 57, CategoryId = 6, Name = "Màn ch?ng côn trùng",        Description = "Màn lu?i ch?ng mu?i và côn trùng khi ng?", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 2.0m,    WeightPerUnit = 0.4m,    CreatedAt = now, UpdatedAt = now },
            // Tái s? d?ng: d?ng c? c?a c?u h? viên (b?t bu?c hoàn tr?)
            new ReliefItem { Id = 58, CategoryId = 6, Name = "B? c?c và dây l?u",          Description = "B? c?c kim lo?i và dây bu?c d? d?ng l?u", Unit = "b?",    ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 3.0m,    WeightPerUnit = 2.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 59, CategoryId = 6, Name = "T?m b?t ch?ng th?m",        Description = "T?m b?t PE dày ch?ng th?m nu?c dùng lót sàn l?u", Unit = "t?m",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 4.0m,    WeightPerUnit = 1.2m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 60, CategoryId = 6, Name = "Dây bu?c da nang",           Description = "Dây th?ng da nang dùng bu?c, c? d?nh v?t d?ng", Unit = "cu?n",  ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 2.0m,    WeightPerUnit = 1.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 61, CategoryId = 6, Name = "Ðèn LED dã chi?n",           Description = "Ðèn LED s?c dùng chi?u sáng dã chi?n", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 1.0m,    WeightPerUnit = 0.35m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 62, CategoryId = 6, Name = "N?n kh?n c?p",               Description = "N?n cháy lâu dùng chi?u sáng khi m?t di?n", Unit = "cây",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.15m,   WeightPerUnit = 0.12m,   CreatedAt = now, UpdatedAt = now },

            // -- Category 7: Công c? s?a ch?a (RepairTools) - 10 items --------
            new ReliefItem { Id = 63, CategoryId = 7, Name = "Búa dóng dinh",                     Description = "Búa s?t dóng dinh dùng s?a ch?a nhà c?a", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 1.5m,    WeightPerUnit = 0.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 64, CategoryId = 7, Name = "Ðinh các lo?i",                     Description = "B? dinh s?t các kích c? dùng s?a ch?a", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.3m,    WeightPerUnit = 0.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 65, CategoryId = 7, Name = "Cua tay da nang",                   Description = "Cua tay g?p g?n dùng c?t g? và v?t li?u", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 3.0m,    WeightPerUnit = 0.6m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 66, CategoryId = 7, Name = "Tua vít 2 d?u",                     Description = "Tua vít 2 d?u d?t và bake dùng s?a ch?a", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.3m,    WeightPerUnit = 0.15m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 67, CategoryId = 7, Name = "Kìm c?t dây",                       Description = "Kìm c?t dây thép và dây di?n da nang", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.5m,    WeightPerUnit = 0.3m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 68, CategoryId = 7, Name = "Bang keo ch?ng th?m",               Description = "Bang keo dán ch?ng th?m nu?c cho mái và tu?ng", Unit = "cu?n",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.2m,    WeightPerUnit = 0.15m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 69, CategoryId = 7, Name = "Dao da nang dã chi?n",              Description = "Dao g?p da nang tích h?p nhi?u công c?", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.2m,    WeightPerUnit = 0.2m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 70, CategoryId = 7, Name = "X?ng tay",                          Description = "X?ng tay g?p g?n dùng dào d?p trong c?u tr?", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 4.0m,    WeightPerUnit = 1.2m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 71, CategoryId = 7, Name = "Bao cát ch?ng lu",                  Description = "Bao cát dùng d?p dê ngan nu?c lu tràn", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 2.5m,    WeightPerUnit = 0.4m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 72, CategoryId = 7, Name = "B? d?ng c? s?a ch?a di?n co b?n",  Description = "B? d?ng c? s?a ch?a di?n g?m kìm, tua vít, bang keo", Unit = "b?",    ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 4.0m,    WeightPerUnit = 2.5m,    CreatedAt = now, UpdatedAt = now },

            // -- Category 8: Thi?t b? c?u h? (RescueEquipment) - 14 items ----
            new ReliefItem { Id = 4,  CategoryId = 8, Name = "Áo phao c?u sinh",              Description = "Áo phao tiêu chu?n ph?c v? c?u h? du?ng th?y", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 8.0m,    WeightPerUnit = 1.2m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 21, CategoryId = 8, Name = "Bình l?c nu?c dã chi?n",        Description = "Bình l?c nu?c di d?ng l?c nu?c b?n thành nu?c s?ch", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 5.0m,    WeightPerUnit = 2.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 23, CategoryId = 8, Name = "Can d?ng nu?c 10L",             Description = "Can nh?a 10 lít ch?a và v?n chuy?n nu?c s?ch", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 12.0m,   WeightPerUnit = 0.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 24, CategoryId = 8, Name = "Túi d?ng nu?c linh ho?t",       Description = "Túi nh?a d?o d?ng nu?c g?p g?n khi không s? d?ng", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 1.5m,    WeightPerUnit = 0.3m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 31, CategoryId = 8, Name = "Nhi?t k? di?n t?",              Description = "Nhi?t k? di?n t? do thân nhi?t nhanh chóng", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.1m,    WeightPerUnit = 0.05m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 73, CategoryId = 8, Name = "Xu?ng cao su c?u h?",           Description = "Xu?ng cao su chuyên d?ng cho nhi?m v? c?u h? lu", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 250.0m,  WeightPerUnit = 45.0m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 74, CategoryId = 8, Name = "Dây th?ng c?u sinh 30m",        Description = "Dây th?ng dài 30m ch?u l?c cao dùng c?u h?", Unit = "cu?n",  ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 6.0m,    WeightPerUnit = 3.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 75, CategoryId = 8, Name = "Phao tròn c?u sinh",            Description = "Phao tròn c?u sinh tiêu chu?n ném cho n?n nhân", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 20.0m,   WeightPerUnit = 2.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 76, CategoryId = 8, Name = "Máy bom nu?c di d?ng",          Description = "Máy bom nu?c ch?y xang di d?ng hút nu?c ng?p", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 60.0m,   WeightPerUnit = 25.0m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 77, CategoryId = 8, Name = "B? dàm liên l?c dã chi?n",      Description = "B? dàm c?m tay liên l?c t?n s? UHF/VHF", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.5m,    WeightPerUnit = 0.3m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 78, CategoryId = 8, Name = "Ðèn tín hi?u kh?n c?p",        Description = "Ðèn tín hi?u nh?p nháy c?nh báo khu v?c nguy hi?m", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.8m,    WeightPerUnit = 0.4m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 79, CategoryId = 8, Name = "Máy phát di?n di d?ng",         Description = "Máy phát di?n xang di d?ng công su?t nh?", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 120.0m,  WeightPerUnit = 50.0m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 80, CategoryId = 8, Name = "Cáng khiêng thuong",            Description = "Cáng g?p g?n dùng v?n chuy?n ngu?i b? thuong", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 30.0m,   WeightPerUnit = 7.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 81, CategoryId = 8, Name = "Mu b?o hi?m c?u h?",           Description = "Mu b?o hi?m chuyên d?ng cho c?u h? viên", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 6.0m,    WeightPerUnit = 0.6m,    CreatedAt = now, UpdatedAt = now },

            // -- Category 9: Su?i ?m (Heating) - 10 items --------------------
            new ReliefItem { Id = 6,  CategoryId = 9, Name = "Chan ?m gi? nhi?t",             Description = "Chan dày gi? nhi?t dùng trong th?i ti?t l?nh", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 6.0m,    WeightPerUnit = 1.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 82, CategoryId = 9, Name = "Than t? ong",                    Description = "Than t? ong dùng d?t su?i ?m ho?c n?u an", Unit = "viên",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 1.2m,    WeightPerUnit = 1.0m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 83, CategoryId = 9, Name = "Máy su?i di?n mini",             Description = "Máy su?i di?n nh? g?n công su?t th?p", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 8.0m,    WeightPerUnit = 2.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 84, CategoryId = 9, Name = "Túi su?i ?m tay dùng m?t l?n",  Description = "Túi su?i ?m tay ph?n ?ng hóa h?c dùng m?t l?n", Unit = "gói",   ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.05m,   WeightPerUnit = 0.04m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 85, CategoryId = 9, Name = "B? qu?n áo nhi?t",               Description = "B? d? lót gi? nhi?t m?c trong th?i ti?t rét", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 2.5m,    WeightPerUnit = 0.4m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 86, CategoryId = 9, Name = "?m dun nu?c du l?ch",            Description = "?m dun nu?c di?n nh? g?n ti?n dùng dã chi?n", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 3.0m,    WeightPerUnit = 0.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 87, CategoryId = 9, Name = "B?p gas du l?ch mini",           Description = "B?p gas mini g?p g?n dùng n?u an dã chi?n", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 4.0m,    WeightPerUnit = 1.5m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 88, CategoryId = 9, Name = "Bình gas mini dã chi?n",         Description = "Bình gas lon nh? dùng cho b?p gas du l?ch", Unit = "bình",  ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.8m,    WeightPerUnit = 0.35m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 89, CategoryId = 9, Name = "Chan di?n su?i",                 Description = "Chan di?n su?i ?m dùng khi ng? mùa l?nh", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 5.0m,    WeightPerUnit = 1.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 90, CategoryId = 9, Name = "T?m su?i ?m b?c x?",            Description = "T?m su?i h?ng ngo?i b?c x? di d?ng", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 15.0m,   WeightPerUnit = 5.0m,    CreatedAt = now, UpdatedAt = now },

            // -- Category 10: Phuong ti?n (Vehicle) - 10 items -----------------
            new ReliefItem { Id = 101, CategoryId = 10, Name = "Xe t?i c?u tr? 2.5 t?n",       Description = "Xe t?i 2.5 t?n v?n chuy?n hàng c?u tr?", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 18000.0m, WeightPerUnit = 3500.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 102, CategoryId = 10, Name = "Xe c?u thuong",                 Description = "Xe chuyên d?ng v?n chuy?n c?p c?u và b?nh nhân", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 16000.0m, WeightPerUnit = 3800.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 103, CategoryId = 10, Name = "Xe bán t?i 4x4",                Description = "Xe bán t?i 2 c?u vu?t d?a hình x?u", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 12000.0m, WeightPerUnit = 2200.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 104, CategoryId = 10, Name = "Xe máy d?a hình",               Description = "Xe máy d?a hình di vào vùng khó ti?p c?n", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 2500.0m,  WeightPerUnit = 150.0m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 105, CategoryId = 10, Name = "Ca nô c?u h?",                  Description = "Ca nô máy chuyên d?ng c?u h? du?ng th?y", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 8000.0m,  WeightPerUnit = 800.0m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 106, CategoryId = 10, Name = "Xe ch? hàng nh? 1 t?n",         Description = "Xe t?i nh? 1 t?n v?n chuy?n hàng c?u tr?", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 14000.0m, WeightPerUnit = 2500.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 107, CategoryId = 10, Name = "Xe t?i dông l?nh 3.5 t?n",      Description = "Xe t?i dông l?nh b?o qu?n th?c ph?m tuoi s?ng", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 20000.0m, WeightPerUnit = 5000.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 108, CategoryId = 10, Name = "Xe khách 16 ch?",               Description = "Xe khách 16 ch? ch? ngu?i so tán", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 15000.0m, WeightPerUnit = 3200.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 109, CategoryId = 10, Name = "Xe c?u di d?ng",                Description = "Xe c?u di d?ng d?n d?p d? nát và v?t c?n", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 20000.0m, WeightPerUnit = 12000.0m, CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 110, CategoryId = 10, Name = "Xe chuyên d?ng phòng cháy",     Description = "Xe ch?a cháy chuyên d?ng phòng cháy ch?a cháy", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(), VolumePerUnit = 18000.0m, WeightPerUnit = 8000.0m, CreatedAt = now, UpdatedAt = now },

            // -- Category 99: Khác (Others) - 10 items ------------------------
            new ReliefItem { Id = 91,  CategoryId = 99, Name = "Pin d? phòng 10000mAh",           Description = "Pin s?c d? phòng 10000mAh s?c di?n tho?i", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.25m,   WeightPerUnit = 0.22m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 92,  CategoryId = 99, Name = "Cáp s?c da nang",                 Description = "Cáp s?c da d?u Lightning/USB-C/Micro USB", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.08m,   WeightPerUnit = 0.04m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 93,  CategoryId = 99, Name = "B?n d? d?a hình kh?n c?p",        Description = "B?n d? in d?a hình khu v?c thu?ng x?y ra thiên tai", Unit = "t?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.1m,    WeightPerUnit = 0.05m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 94,  CategoryId = 99, Name = "Còi báo d?ng kh?n c?p",           Description = "Còi th?i báo d?ng và kêu g?i c?u h? kh?n c?p", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.02m,   WeightPerUnit = 0.015m,  CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 95,  CategoryId = 99, Name = "Kính b?o h? lao d?ng",            Description = "Kính b?o h? ch?ng b?i và m?nh v? khi làm vi?c", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.3m,    WeightPerUnit = 0.08m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 96,  CategoryId = 99, Name = "Ba lô kh?n c?p",                  Description = "Ba lô ch?a d? dùng thi?t y?u cho tình hu?ng kh?n c?p", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 25.0m,   WeightPerUnit = 0.8m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 97,  CategoryId = 99, Name = "S? tay và bút ghi chép",          Description = "B? s? tay và bút bi dùng ghi chép thông tin hi?n tru?ng", Unit = "b?",    ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.3m,    WeightPerUnit = 0.18m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 98,  CategoryId = 99, Name = "B? dèn pin d?i d?u",              Description = "Ðèn pin LED d?i d?u r?i sáng r?nh tay", Unit = "b?",    ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 0.5m,    WeightPerUnit = 0.15m,   CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 99,  CategoryId = 99, Name = "Áo ph?n quang an toàn",           Description = "Áo ghi lê ph?n quang tang nh?n di?n trong dêm", Unit = "chi?c", ItemType = ItemType.Reusable.ToString(),   VolumePerUnit = 1.5m,    WeightPerUnit = 0.2m,    CreatedAt = now, UpdatedAt = now },
            new ReliefItem { Id = 100, CategoryId = 99, Name = "Pháo sáng kh?n c?p",              Description = "Pháo sáng phát tín hi?u c?u c?u kh?n c?p", Unit = "chi?c", ItemType = ItemType.Consumable.ToString(), VolumePerUnit = 0.25m,   WeightPerUnit = 0.15m,   CreatedAt = now, UpdatedAt = now }
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
        //   - Basic food/water/blankets ? all civilian groups + Rescuer (field use)
        //   - Paediatric/elderly/maternal items ? specific vulnerable groups only
        //   - Medical consumables ? civilian groups + Rescuer (field first-aid)
        //   - Hygiene consumables ? relevant civilian groups + Rescuer (field hygiene)
        //   - Raincoats / boots ? Adult + Rescuer (rescuers wear them too)
        //   - Reusable rescue equipment ? Rescuer only
        //   - Vehicles ? Rescuer only

        modelBuilder.Entity("item_model_target_groups").HasData(
            // -- Category 1: Th?c ph?m -----------------------------------------
            // Mì tôm – d? n?u, rescuer dùng trong hi?n tru?ng
            new { item_model_id = 1,  target_group_id = 4 }, // Adult
            new { item_model_id = 1,  target_group_id = 5 }, // Rescuer
            // S?a b?t tr? em – ch? dành cho tr?
            new { item_model_id = 7,  target_group_id = 1 }, // Children
            // Luong khô - kh?u ph?n dã chi?n cho rescuer và adult
            new { item_model_id = 8,  target_group_id = 4 }, // Adult
            new { item_model_id = 8,  target_group_id = 5 }, // Rescuer
            // G?o s?y khô – th?c ph?m co b?n, dùng cho nhi?u nhóm
            new { item_model_id = 11, target_group_id = 4 }, // Adult
            new { item_model_id = 11, target_group_id = 2 }, // Elderly
            new { item_model_id = 11, target_group_id = 3 }, // Pregnant
            new { item_model_id = 11, target_group_id = 5 }, // Rescuer
            // Cháo an li?n – m?m, d? tiêu, phù h?p ngu?i già / bà b?u / tr? em
            new { item_model_id = 12, target_group_id = 2 }, // Elderly
            new { item_model_id = 12, target_group_id = 1 }, // Children
            new { item_model_id = 12, target_group_id = 3 }, // Pregnant
            // Bánh mì khô – ti?n l?i ngoài hi?n tru?ng
            new { item_model_id = 13, target_group_id = 4 }, // Adult
            new { item_model_id = 13, target_group_id = 5 }, // Rescuer
            // Mu?i tinh – gia v? co b?n
            new { item_model_id = 14, target_group_id = 4 }, // Adult
            // Ðu?ng cát tr?ng – gia v? co b?n
            new { item_model_id = 15, target_group_id = 4 }, // Adult
            // D?u an th?c v?t – gia v? co b?n
            new { item_model_id = 16, target_group_id = 4 }, // Adult
            // Th?t h?p dóng gói – ngu?n protein, rescuer dùng ngoài hi?n tru?ng
            new { item_model_id = 17, target_group_id = 4 }, // Adult
            new { item_model_id = 17, target_group_id = 5 }, // Rescuer

            // -- Category 2: Nu?c u?ng -----------------------------------------
            // Nu?c tinh khi?t – thi?t y?u cho t?t c?
            new { item_model_id = 2,  target_group_id = 4 }, // Adult
            new { item_model_id = 2,  target_group_id = 1 }, // Children
            new { item_model_id = 2,  target_group_id = 2 }, // Elderly
            new { item_model_id = 2,  target_group_id = 3 }, // Pregnant
            new { item_model_id = 2,  target_group_id = 5 }, // Rescuer
            // Nu?c l?c bình 20L – dùng chung cho c?ng d?ng
            new { item_model_id = 18, target_group_id = 4 }, // Adult
            // Viên l?c nu?c kh?n c?p – rescuer l?c nu?c t?i hi?n tru?ng
            new { item_model_id = 19, target_group_id = 4 }, // Adult
            new { item_model_id = 19, target_group_id = 5 }, // Rescuer
            // Nu?c dóng thùng 24 chai
            new { item_model_id = 20, target_group_id = 4 }, // Adult
            // Nu?c khoáng thiên nhiên 500ml
            new { item_model_id = 22, target_group_id = 4 }, // Adult
            // Nu?c d?a dóng h?p
            new { item_model_id = 25, target_group_id = 4 }, // Adult
            // B?t bù di?n gi?i ORS – quan tr?ng cho m?i nhóm khi m?t nu?c
            new { item_model_id = 26, target_group_id = 4 }, // Adult
            new { item_model_id = 26, target_group_id = 1 }, // Children
            new { item_model_id = 26, target_group_id = 2 }, // Elderly
            new { item_model_id = 26, target_group_id = 3 }, // Pregnant
            new { item_model_id = 26, target_group_id = 5 }, // Rescuer

            // -- Category 3: Y t? ----------------------------------------------
            // Thu?c h? s?t Paracetamol 500mg - dùng r?ng rãi, k? c? rescuer
            new { item_model_id = 3,  target_group_id = 4 }, // Adult
            new { item_model_id = 3,  target_group_id = 2 }, // Elderly
            new { item_model_id = 3,  target_group_id = 5 }, // Rescuer
            // D?u gió – gi?m dau, ch?ng l?nh; ngu?i l?n tu?i và adult d?u dùng
            new { item_model_id = 9,  target_group_id = 2 }, // Elderly
            new { item_model_id = 9,  target_group_id = 4 }, // Adult
            // S?t & Vitamin t?ng h?p – ch? y?u cho bà b?u
            new { item_model_id = 10, target_group_id = 3 }, // Pregnant
            // Bang g?c y t? vô khu?n – so c?u n?n nhân và c?u h? viên b? thuong
            new { item_model_id = 27, target_group_id = 4 }, // Adult
            new { item_model_id = 27, target_group_id = 5 }, // Rescuer
            // Bông gòn y t? – so c?u cho c? n?n nhân l?n rescuer
            new { item_model_id = 28, target_group_id = 4 }, // Adult
            new { item_model_id = 28, target_group_id = 5 }, // Rescuer
            // Thu?c kháng sinh Amoxicillin – kê don, dành cho adult
            new { item_model_id = 29, target_group_id = 4 }, // Adult
            // Dung d?ch sát khu?n Betadine – rescuer c?n sát khu?n v?t thuong ngoài hi?n tru?ng
            new { item_model_id = 30, target_group_id = 4 }, // Adult
            new { item_model_id = 30, target_group_id = 5 }, // Rescuer
            // Kh?u trang y t? 3 l?p – b?o v? cho t?t c? các nhóm
            new { item_model_id = 32, target_group_id = 4 }, // Adult
            new { item_model_id = 32, target_group_id = 1 }, // Children
            new { item_model_id = 32, target_group_id = 2 }, // Elderly
            new { item_model_id = 32, target_group_id = 3 }, // Pregnant
            new { item_model_id = 32, target_group_id = 5 }, // Rescuer
            // B? so c?u co b?n – rescuer mang theo trong nhi?m v?
            new { item_model_id = 33, target_group_id = 4 }, // Adult
            new { item_model_id = 33, target_group_id = 5 }, // Rescuer

            // -- Category 4: V? sinh cá nhân -----------------------------------
            // Bang v? sinh – ph? n? adult và bà b?u
            new { item_model_id = 5,  target_group_id = 4 }, // Adult
            new { item_model_id = 5,  target_group_id = 3 }, // Pregnant
            // Xà phòng di?t khu?n
            new { item_model_id = 34, target_group_id = 4 }, // Adult
            // Nu?c r?a tay khô – rescuer c?n gi? v? sinh ngoài hi?n tru?ng
            new { item_model_id = 35, target_group_id = 4 }, // Adult
            new { item_model_id = 35, target_group_id = 5 }, // Rescuer
            // Khan u?t kháng khu?n – ti?n cho tr? em và rescuer
            new { item_model_id = 36, target_group_id = 4 }, // Adult
            new { item_model_id = 36, target_group_id = 1 }, // Children
            new { item_model_id = 36, target_group_id = 5 }, // Rescuer
            // Kem dánh rang
            new { item_model_id = 37, target_group_id = 4 }, // Adult
            // Bàn ch?i dánh rang
            new { item_model_id = 38, target_group_id = 4 }, // Adult
            // D?u g?i d?u
            new { item_model_id = 39, target_group_id = 4 }, // Adult
            // Khan bông t?m
            new { item_model_id = 40, target_group_id = 4 }, // Adult
            // Gi?y v? sinh
            new { item_model_id = 41, target_group_id = 4 }, // Adult
            // Tã dùng m?t l?n - tr? em là ch? y?u
            new { item_model_id = 42, target_group_id = 1 }, // Children

            // -- Category 5: Qu?n áo -------------------------------------------
            // Áo mua ngu?i l?n – rescuer m?c khi tác nghi?p
            new { item_model_id = 43, target_group_id = 4 }, // Adult
            new { item_model_id = 43, target_group_id = 5 }, // Rescuer
            // ?ng cao su ch?ng lu – rescuer di chuy?n vùng ng?p
            new { item_model_id = 44, target_group_id = 4 }, // Adult
            new { item_model_id = 44, target_group_id = 5 }, // Rescuer
            // B? qu?n áo tr? em
            new { item_model_id = 45, target_group_id = 1 }, // Children
            // Áo ?m ngu?i l?n – bà b?u và ngu?i cao tu?i cung c?n áo ?m
            new { item_model_id = 46, target_group_id = 4 }, // Adult
            new { item_model_id = 46, target_group_id = 2 }, // Elderly
            new { item_model_id = 46, target_group_id = 3 }, // Pregnant
            // B? qu?n áo ngu?i l?n – cung phù h?p ngu?i già
            new { item_model_id = 47, target_group_id = 4 }, // Adult
            new { item_model_id = 47, target_group_id = 2 }, // Elderly
            // B? qu?n áo ngu?i cao tu?i
            new { item_model_id = 48, target_group_id = 2 }, // Elderly
            // Gang tay gi? ?m
            new { item_model_id = 49, target_group_id = 4 }, // Adult
            // T?t len gi? ?m
            new { item_model_id = 50, target_group_id = 4 }, // Adult
            // Mu len
            new { item_model_id = 51, target_group_id = 4 }, // Adult
            // Áo mua tr? em
            new { item_model_id = 52, target_group_id = 1 }, // Children

            // -- Category 6: Noi trú ?n ---------------------------------------
            // L?u b?t c?u tr? 4 ngu?i – c?p cho n?n nhân, rescuer cung d?ng tr?i
            new { item_model_id = 53, target_group_id = 4 }, // Adult
            new { item_model_id = 53, target_group_id = 5 }, // Rescuer
            // T?m b?t che mua da nang
            new { item_model_id = 54, target_group_id = 4 }, // Adult
            // Túi ng? gi? nhi?t
            new { item_model_id = 55, target_group_id = 4 }, // Adult
            // Ð?m hoi dã chi?n
            new { item_model_id = 56, target_group_id = 4 }, // Adult
            // Màn ch?ng côn trùng
            new { item_model_id = 57, target_group_id = 4 }, // Adult
            // B? c?c và dây l?u – Reusable, dùng b?i rescuer
            new { item_model_id = 58, target_group_id = 5 }, // Rescuer
            // T?m b?t ch?ng th?m – rescuer ph? thi?t b? ngoài hi?n tru?ng
            new { item_model_id = 59, target_group_id = 4 }, // Adult
            new { item_model_id = 59, target_group_id = 5 }, // Rescuer
            // Dây bu?c da nang – Reusable
            new { item_model_id = 60, target_group_id = 5 }, // Rescuer
            // Ðèn LED dã chi?n - Reusable
            new { item_model_id = 61, target_group_id = 5 }, // Rescuer
            // N?n kh?n c?p – rescuer cung dùng chi?u sáng t?m th?i
            new { item_model_id = 62, target_group_id = 4 }, // Adult
            new { item_model_id = 62, target_group_id = 5 }, // Rescuer

            // -- Category 7: Công c? s?a ch?a ---------------------------------
            // Búa dóng dinh – Reusable
            new { item_model_id = 63, target_group_id = 5 }, // Rescuer
            // Ðinh các lo?i – Consumable, rescuer s?a ch?a công trình kh?n c?p
            new { item_model_id = 64, target_group_id = 5 }, // Rescuer
            // Cua tay da nang – Reusable
            new { item_model_id = 65, target_group_id = 5 }, // Rescuer
            // Tua vít 2 d?u – Reusable
            new { item_model_id = 66, target_group_id = 5 }, // Rescuer
            // Kìm c?t dây – Reusable
            new { item_model_id = 67, target_group_id = 5 }, // Rescuer
            // Bang keo ch?ng th?m – Consumable, dùng trong hi?n tru?ng
            new { item_model_id = 68, target_group_id = 5 }, // Rescuer
            // Dao da nang dã chi?n - Reusable
            new { item_model_id = 69, target_group_id = 5 }, // Rescuer
            // X?ng tay – Reusable
            new { item_model_id = 70, target_group_id = 5 }, // Rescuer
            // Bao cát ch?ng lu – Reusable
            new { item_model_id = 71, target_group_id = 5 }, // Rescuer
            // B? d?ng c? s?a ch?a di?n co b?n – Reusable
            new { item_model_id = 72, target_group_id = 5 }, // Rescuer

            // -- Category 8: Thi?t b? c?u h? (t?t c? Reusable, ch? Rescuer) --
            new { item_model_id = 4,  target_group_id = 5 }, // Áo phao c?u sinh
            new { item_model_id = 21, target_group_id = 5 }, // Bình l?c nu?c dã chi?n
            new { item_model_id = 23, target_group_id = 5 }, // Can d?ng nu?c 10L
            new { item_model_id = 24, target_group_id = 5 }, // Túi d?ng nu?c linh ho?t
            new { item_model_id = 31, target_group_id = 5 }, // Nhi?t k? di?n t?
            new { item_model_id = 73, target_group_id = 5 }, // Xu?ng cao su c?u h?
            new { item_model_id = 74, target_group_id = 5 }, // Dây th?ng c?u sinh 30m
            new { item_model_id = 75, target_group_id = 5 }, // Phao tròn c?u sinh
            new { item_model_id = 76, target_group_id = 5 }, // Máy bom nu?c di d?ng
            new { item_model_id = 77, target_group_id = 5 }, // B? dàm liên l?c dã chi?n
            new { item_model_id = 78, target_group_id = 5 }, // Ðèn tín hi?u kh?n c?p
            new { item_model_id = 79, target_group_id = 5 }, // Máy phát di?n di d?ng
            new { item_model_id = 80, target_group_id = 5 }, // Cáng khiêng thuong
            new { item_model_id = 81, target_group_id = 5 }, // Mu b?o hi?m c?u h?

            // -- Category 9: Su?i ?m ------------------------------------------
            // Chan ?m gi? nhi?t – thi?t y?u cho t?t c? nhóm dân s? d? t?n thuong
            new { item_model_id = 6,  target_group_id = 4 }, // Adult
            new { item_model_id = 6,  target_group_id = 1 }, // Children
            new { item_model_id = 6,  target_group_id = 2 }, // Elderly
            new { item_model_id = 6,  target_group_id = 3 }, // Pregnant
            // Than t? ong
            new { item_model_id = 82, target_group_id = 4 }, // Adult
            // Máy su?i di?n mini
            new { item_model_id = 83, target_group_id = 4 }, // Adult
            // Túi su?i ?m tay dùng m?t l?n – rescuer gi? ?m khi tác nghi?p dêm
            new { item_model_id = 84, target_group_id = 4 }, // Adult
            new { item_model_id = 84, target_group_id = 5 }, // Rescuer
            // B? qu?n áo nhi?t
            new { item_model_id = 85, target_group_id = 4 }, // Adult
            // ?m dun nu?c du l?ch
            new { item_model_id = 86, target_group_id = 4 }, // Adult
            // B?p gas du l?ch mini - rescuer n?u an ngoài dã chi?n
            new { item_model_id = 87, target_group_id = 4 }, // Adult
            new { item_model_id = 87, target_group_id = 5 }, // Rescuer
            // Bình gas mini dã chi?n - kèm b?p, rescuer dùng tr?c ti?p
            new { item_model_id = 88, target_group_id = 4 }, // Adult
            new { item_model_id = 88, target_group_id = 5 }, // Rescuer
            // Chan di?n su?i
            new { item_model_id = 89, target_group_id = 4 }, // Adult
            // T?m su?i ?m b?c x?
            new { item_model_id = 90, target_group_id = 4 }, // Adult

            // -- Category 10: Phuong ti?n (Reusable, ch? Rescuer) -------------
            new { item_model_id = 101, target_group_id = 5 }, // Xe t?i c?u tr? 2.5 t?n
            new { item_model_id = 102, target_group_id = 5 }, // Xe c?u thuong
            new { item_model_id = 103, target_group_id = 5 }, // Xe bán t?i 4x4
            new { item_model_id = 104, target_group_id = 5 }, // Xe máy d?a hình
            new { item_model_id = 105, target_group_id = 5 }, // Ca nô c?u h?
            new { item_model_id = 106, target_group_id = 5 }, // Xe ch? hàng nh? 1 t?n
            new { item_model_id = 107, target_group_id = 5 }, // Xe t?i dông l?nh 3.5 t?n
            new { item_model_id = 108, target_group_id = 5 }, // Xe khách 16 ch?
            new { item_model_id = 109, target_group_id = 5 }, // Xe c?u di d?ng
            new { item_model_id = 110, target_group_id = 5 }, // Xe chuyên d?ng phòng cháy

            // -- Category 99: Khác ---------------------------------------------
            // Pin d? phòng – rescuer c?n s?c thi?t b? liên l?c
            new { item_model_id = 91,  target_group_id = 4 }, // Adult
            new { item_model_id = 91,  target_group_id = 5 }, // Rescuer
            // Cáp s?c da nang – rescuer gi? thi?t b? ho?t d?ng
            new { item_model_id = 92,  target_group_id = 4 }, // Adult
            new { item_model_id = 92,  target_group_id = 5 }, // Rescuer
            // B?n d? d?a hình kh?n c?p – ch? y?u rescuer
            new { item_model_id = 93,  target_group_id = 5 }, // Rescuer
            // Còi báo d?ng kh?n c?p – phát tín hi?u c?u c?u, rescuer cung dùng
            new { item_model_id = 94,  target_group_id = 4 }, // Adult
            new { item_model_id = 94,  target_group_id = 5 }, // Rescuer
            // Kính b?o h? lao d?ng – Reusable, rescuer
            new { item_model_id = 95,  target_group_id = 5 }, // Rescuer
            // Ba lô kh?n c?p – rescuer mang thi?t b? vào hi?n tru?ng
            new { item_model_id = 96,  target_group_id = 4 }, // Adult
            new { item_model_id = 96,  target_group_id = 5 }, // Rescuer
            // S? tay và bút ghi chép
            new { item_model_id = 97,  target_group_id = 4 }, // Adult
            // B? dèn pin d?i d?u – Reusable
            new { item_model_id = 98,  target_group_id = 5 }, // Rescuer
            // Áo ph?n quang an toàn – Reusable
            new { item_model_id = 99,  target_group_id = 5 }, // Rescuer
            // Pháo sáng kh?n c?p – rescuer báo hi?u v? trí
            new { item_model_id = 100, target_group_id = 5 }  // Rescuer
        );
    }

    private static void SeedDepots(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // CurrentUtilization = S (quantity × volumePerUnit) across all items (consumable + reusable + vehicle)
        // CurrentWeightUtilization = S (quantity × weightPerUnit) across all items
        // Volume unit: dm³ (cubic decimeters), Weight unit: kg
        // Depot 1 (Hu?):        Volume=831777.9,  Weight=330877.49  ? Capacity 1100000 (~75.6%), WeightCapacity 440000 (~75.2%)
        // Depot 2 (Ðà N?ng):    Volume=754700.9,  Weight=365265.69  ? Capacity 1000000 (~75.5%), WeightCapacity 480000 (~76.1%)
        // Depot 3 (Hà Tinh):    Volume=443207.6,  Weight=195723.64  ? Capacity 600000  (~73.9%), WeightCapacity 260000 (~75.3%)
        // Depot 4 (HN/TW):      Volume=1064369.2, Weight=472365.44  ? Capacity 1400000 (~76.0%), WeightCapacity 650000 (~72.7%)
        // Depot 5 (Thang Bình): Volume=1890,      Weight=581       ? closure test: external resolution
        // Depot 6 (Qu?ng Ninh): Volume=2400,      Weight=732.5     ? closure test: transfer to another depot
        // Depot 7 (Vinh):       Volume=0,         Weight=0         ? closure test: empty depot / transfer target
        modelBuilder.Entity<Depot>().HasData(
            new Depot { Id = 1, Name = "U? Ban MTTQVN T?nh Th?a Thiên Hu?", Address = "46 Ð?ng Ða, TP. Hu?, Th?a Thiên Hu?", Location = new Point(107.56799781003454, 16.454572773043417) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 1100000m,  CurrentUtilization = 831777.9m,  WeightCapacity = 440000m,  CurrentWeightUtilization = 330877.49m, AdvanceLimit = 80_000_000m,  OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg" },
            new Depot { Id = 2, Name = "?y ban MTTQVN TP Ðà N?ng", Address = "270 Trung N? Vuong, H?i Châu, Ðà N?ng", Location = new Point(108.22283205420794, 16.080298466000496) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 1000000m,  CurrentUtilization = 754700.9m,  WeightCapacity = 480000m,  CurrentWeightUtilization = 365265.69m, AdvanceLimit = 60_000_000m,  OutstandingAdvanceAmount = 10_000_000m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 3, Name = "?y Ban MTTQ T?nh Hà Tinh", Address = "72 Phan Ðình Phùng, TP. Hà Tinh, Hà Tinh", Location = new Point(105.90102499916586, 18.349622333272194) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 600000m,   CurrentUtilization = 443207.6m,  WeightCapacity = 260000m,  CurrentWeightUtilization = 195723.64m, AdvanceLimit = 40_000_000m,  OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg" },
            new Depot { Id = 4, Name = "?y ban MTTQVN Vi?t Nam", Address = "46 Tràng Thi, Hoàn Ki?m, Hà N?i", Location = new Point(105.842191, 21.027819) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 1400000m,  CurrentUtilization = 1064369.2m, WeightCapacity = 650000m,  CurrentWeightUtilization = 472365.44m, AdvanceLimit = 100_000_000m, OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 5, Name = "?y ban MTTQVN Huy?n Thang Bình", Address = "282 Ti?u La, th? tr?n Hà Lam, huy?n Thang Bình, Qu?ng Nam", Location = new Point(108.4587, 15.6949) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 250000m, CurrentUtilization = 1890m, WeightCapacity = 120000m, CurrentWeightUtilization = 581m, AdvanceLimit = 12_000_000m, OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 6, Name = "?y ban MTTQVN Huy?n Qu?ng Ninh", Address = "TT. Quán Hàu, huy?n Qu?ng Ninh, Qu?ng Bình", Location = new Point(106.6175, 17.4619) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 280000m, CurrentUtilization = 2400m, WeightCapacity = 140000m, CurrentWeightUtilization = 732.5m, AdvanceLimit = 14_000_000m, OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" },
            new Depot { Id = 7, Name = "?y ban MTTQVN T?nh Ngh? An", Address = "1 Phan Ðang Luu, TP. Vinh, Ngh? An", Location = new Point(105.6936046, 18.6732581) { SRID = 4326 }, Status = DepotStatus.Available.ToString(), Capacity = 300000m, CurrentUtilization = 0m, WeightCapacity = 150000m, CurrentWeightUtilization = 0m, AdvanceLimit = 5_000_000m, OutstandingAdvanceAmount = 0m, LastUpdatedAt = now, ImageUrl = "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg" }
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
            new DepotManager { Id = 5, DepotId = 5, UserId = SeedConstants.Manager5UserId,  AssignedAt = now },
            new DepotManager { Id = 6, DepotId = 6, UserId = SeedConstants.Manager6UserId,  AssignedAt = now },
            new DepotManager { Id = 7, DepotId = 7, UserId = SeedConstants.Manager7UserId,  AssignedAt = now }
        );
    }

    // -- Consumable items ? tracked by quantity in depot_supply_inventory ------
    private static void SeedDepotInventories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        // 72 consumable relief item IDs (same order as before - preserves DSI IDs)
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

        // -- Per-category, per-depot quantity factors -------------------------
        // Depot order: D1-Hu?(0), D2-Ðà N?ng(1), D3-Hà Tinh(2), D4-HN-TW(3)
        // Low factors (< 0.4) are intentional to create supply shortage test data
        var categoryFactors = new Dictionary<int, double[]>
        {
            [1]  = new[] { 1.2,  0.7,  0.25, 1.5  }, // Food       - D3 LOW ??
            [2]  = new[] { 0.8,  1.3,  0.6,  1.4  }, // Water
            [3]  = new[] { 0.25, 1.4,  0.7,  1.5  }, // Medical    - D1 LOW ??
            [4]  = new[] { 0.9,  1.2,  0.2,  1.3  }, // Hygiene    - D3 LOW ??
            [5]  = new[] { 1.1,  0.3,  1.3,  0.9  }, // Clothing   - D2 LOW ??
            [6]  = new[] { 1.0,  0.6,  1.2,  0.8  }, // Shelter
            [7]  = new[] { 1.0,  0.8,  1.0,  1.2  }, // RepairTools
            [9]  = new[] { 0.35, 0.3,  1.5,  0.8  }, // Heating    - D1+D2 LOW ??
            [99] = new[] { 0.6,  0.8,  0.3,  1.2  }, // Others     - D3 LOW ??
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

        // -- Low-stock seed overrides -----------------------------------------
        // DSI ID formula: id = depotIndex * 72 + itemIndex + 1
        //   D1 (Hu?):    IDs  1- 72  | D2 (Ðà N?ng): IDs  73-144
        //   D3 (Hà Tinh): IDs 145-216 | D4 (HN-TW):   IDs 217-288
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

        // -- Transfer reservation overrides - kh?p v?i supply requests dã seed --
        // Request #1 (Depot 1 = Hu? là kho ngu?n, tr?ng thái Accepted):
        //   DSI 1 = Depot 1, Item #1  (Mì tôm)        ? d?t tr? 6000
        //   DSI 2 = Depot 1, Item #2  (Nu?c tinh khi?t) ? d?t tr? 4000
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

        list.AddRange(
            [
                new DepotSupplyInventory
                {
                    Id = 289,
                    DepotId = 5,
                    ItemModelId = 1,
                    Quantity = 1200,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc)
                },
                new DepotSupplyInventory
                {
                    Id = 290,
                    DepotId = 5,
                    ItemModelId = 2,
                    Quantity = 800,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 2, 18, 9, 0, 0, DateTimeKind.Utc)
                },
                new DepotSupplyInventory
                {
                    Id = 291,
                    DepotId = 5,
                    ItemModelId = 43,
                    Quantity = 300,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc)
                },
                new DepotSupplyInventory
                {
                    Id = 292,
                    DepotId = 6,
                    ItemModelId = 1,
                    Quantity = 1500,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc)
                },
                new DepotSupplyInventory
                {
                    Id = 293,
                    DepotId = 6,
                    ItemModelId = 2,
                    Quantity = 1000,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc)
                },
                new DepotSupplyInventory
                {
                    Id = 294,
                    DepotId = 6,
                    ItemModelId = 43,
                    Quantity = 400,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = new DateTime(2026, 3, 6, 8, 30, 0, DateTimeKind.Utc)
                }
            ]);

        modelBuilder.Entity<DepotSupplyInventory>().HasData(list.ToArray());
    }

    // -- Reusable items ? each physical unit tracked individually --------------
    private static void SeedDepotReusableItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        var available = ReusableItemStatus.Available.ToString();
        var good = ReusableItemCondition.Good.ToString();
        var fair = ReusableItemCondition.Fair.ToString();

        // -- Reusable item groups with per-depot unit counts ---------------
        // Per-depot unit table (D1-Hu?, D2-Ðà N?ng, D3-Hà Tinh, D4-HN-TW)
        //
        // RescueEquipment (4,21,23,24,31,73,74,75,76,77,78,79,80,81) : 3, 4, 2, 3
        // Shelter reusables (58,60,61)                                : 5, 2, 4, 3  (Shelter-rich for Hu?+Hà Tinh)
        // RepairTools (63,65,66,67,69,70,71,72)                       : 2, 4, 2, 3
        // Cat99 reusables (95,98,99)                                  : 3, 3, 3, 3
        //
        // Heating reusables: none (all heating is consumable)
        // Vehicle (101-110) : scaled by depot factor ×1.0/×0.8/×0.6/×1.2

        // (itemId, unitsPerDepot[D1,D2,D3,D4])
        var reusableGroups = new (int[] ids, int[] units)[]
        {
            // RescueEquipment - D3 slightly lower
            (new[] { 4, 21, 23, 24, 31, 73, 74, 75, 76, 77, 78, 79, 80, 81 }, new[] { 3, 4, 2, 3 }),
            // Shelter reusables - D1 & D3 high (coastal/flood zones)
            (new[] { 58, 60, 61 }, new[] { 5, 2, 4, 3 }),
            // RepairTools reusables - D2 high (urban center)
            (new[] { 63, 65, 66, 67, 69, 70, 71, 72 }, new[] { 2, 4, 2, 3 }),
            // Category 99 reusables
            (new[] { 95, 98, 99 }, new[] { 3, 3, 3, 3 }),
        };

        // -- Vehicle item IDs and base units per depot ----------------------
        // 101 Xe t?i 2.5T, 102 Xe c?u thuong, 103 Xe bán t?i 4×4, 104 Xe máy d?a hình,
        // 105 Ca nô, 106 Xe ch? hàng 1T, 107 Xe dông l?nh, 108 Xe khách 16 ch?,
        // 109 Xe c?u, 110 Xe PCCC
        int[] vehicleIds       = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
        int[] vehicleBaseUnits = {   5,   3,   5,   8,   4,   5,   3,   3,   2,   2 };
        double[] vehicleDepotFactors = { 1.0, 0.8, 0.6, 1.2 };

        int[] depotIds = { 1, 2, 3, 4 };

        var list = new List<DepotReusableItem>();
        int id = 1;

        for (int d = 0; d < depotIds.Length; d++)
        {
            // -- Non-vehicle reusable items --
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

            // -- Vehicle items: variable units per type, scaled by depot factor --
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

        modelBuilder.Entity<DepotReusableItem>().HasData(list.ToArray());
    }

    // -- Lots for consumable imports -----------------------------------------
    private static void SeedSupplyInventoryLots(ModelBuilder modelBuilder)
    {
        // Each Import inventory-log gets a corresponding lot.
        // Lot Id == InventoryLog Id for simplicity (they share the same auto-sequence space in seed only).
        // RemainingQuantity = QuantityChange for seed data (nothing consumed yet).
        //
        // Log Id ? DSI Id, Qty, SourceType, SourceId, ReceivedDate, ExpiredDate
        // We give realistic expiry dates: food ~6-12 months, medicine ~2 years, toiletries ~18 months, etc.

        var baseDate = new DateTime(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        var lots = new List<SupplyInventoryLot>
        {
            // -- Initial import (Depot 1 - Hu?) -----------------------------
            new() { Id = 1,  SupplyInventoryId = 1,  Quantity = 50000, RemainingQuantity = 50000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  CreatedAt = baseDate },
            new() { Id = 2,  SupplyInventoryId = 2,  Quantity = 40000, RemainingQuantity = 40000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  CreatedAt = baseDate },
            new() { Id = 3,  SupplyInventoryId = 3,  Quantity = 80000, RemainingQuantity = 80000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 4,  SupplyInventoryId = 4,  Quantity = 15000, RemainingQuantity = 15000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 5,  SupplyInventoryId = 5,  Quantity = 8000,  RemainingQuantity = 8000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 6,  SupplyInventoryId = 6,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 7,  SupplyInventoryId = 7,  Quantity = 5000,  RemainingQuantity = 5000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 8,  SupplyInventoryId = 8,  Quantity = 20000, RemainingQuantity = 20000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // -- Initial import (Depot 2 - Ðà N?ng) -------------------------
            new() { Id = 9,  SupplyInventoryId = 45, Quantity = 40000, RemainingQuantity = 40000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 10, SupplyInventoryId = 46, Quantity = 32000, RemainingQuantity = 32000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 11, SupplyInventoryId = 47, Quantity = 64000, RemainingQuantity = 64000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  CreatedAt = baseDate },
            new() { Id = 12, SupplyInventoryId = 48, Quantity = 12000, RemainingQuantity = 12000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 13, SupplyInventoryId = 49, Quantity = 6400,  RemainingQuantity = 6400,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 14, SupplyInventoryId = 50, Quantity = 24000, RemainingQuantity = 24000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },

            // -- Initial import (Depot 3 - Hà Tinh) -------------------------
            new() { Id = 15, SupplyInventoryId = 89,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 16, SupplyInventoryId = 90,  Quantity = 24000, RemainingQuantity = 24000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2,  CreatedAt = baseDate },
            new() { Id = 17, SupplyInventoryId = 91,  Quantity = 48000, RemainingQuantity = 48000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 18, SupplyInventoryId = 92,  Quantity = 9000,  RemainingQuantity = 9000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 19, SupplyInventoryId = 93,  Quantity = 4800,  RemainingQuantity = 4800,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },
            new() { Id = 20, SupplyInventoryId = 95,  Quantity = 3000,  RemainingQuantity = 3000,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 21, SupplyInventoryId = 96,  Quantity = 12000, RemainingQuantity = 12000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // -- Initial import (Depot 4 - MTTQVN) --------------------------
            new() { Id = 22, SupplyInventoryId = 133, Quantity = 45000, RemainingQuantity = 45000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(12), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 23, SupplyInventoryId = 134, Quantity = 36000, RemainingQuantity = 36000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 24, SupplyInventoryId = 135, Quantity = 72000, RemainingQuantity = 72000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  CreatedAt = baseDate },
            new() { Id = 25, SupplyInventoryId = 136, Quantity = 13500, RemainingQuantity = 13500, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  CreatedAt = baseDate },
            new() { Id = 26, SupplyInventoryId = 137, Quantity = 7200,  RemainingQuantity = 7200,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(6),  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },
            new() { Id = 27, SupplyInventoryId = 138, Quantity = 27000, RemainingQuantity = 27000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(24), SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  CreatedAt = baseDate },
            new() { Id = 28, SupplyInventoryId = 139, Quantity = 4500,  RemainingQuantity = 4500,  ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(36), SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  CreatedAt = baseDate },
            new() { Id = 29, SupplyInventoryId = 140, Quantity = 18000, RemainingQuantity = 18000, ReceivedDate = baseDate, ExpiredDate = baseDate.AddMonths(18), SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, CreatedAt = baseDate },

            // -- Purchase imports (Depot 1 - Hu?) ---------------------------
            // Log 33: Invoice 1, mì tôm, Jan 2025
            new() { Id = 30, SupplyInventoryId = 1,  Quantity = 20000, RemainingQuantity = 20000, ReceivedDate = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc) },
            // Log 34: Invoice 1, nu?c, Jan 2025
            new() { Id = 31, SupplyInventoryId = 2,  Quantity = 15000, RemainingQuantity = 15000, ReceivedDate = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc) },

            // -- Donation imports --------------------------------------------
            // Log 36: thu?c, Jun 2025
            new() { Id = 32, SupplyInventoryId = 3,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 6, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 1, CreatedAt = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc) },
            // Log 38: s?a b?t, Oct 2025
            new() { Id = 33, SupplyInventoryId = 5,  Quantity = 1000,  RemainingQuantity = 1000,  ReceivedDate = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, CreatedAt = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc) },

            // -- Purchase imports (Jan & Feb 2026) ---------------------------
            // Log 40: Invoice 2, thu?c, Jan 2026
            new() { Id = 34, SupplyInventoryId = 3,  Quantity = 30000, RemainingQuantity = 30000, ReceivedDate = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2028, 1, 8, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 2, CreatedAt = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc) },
            // Log 42: Invoice 3, d?u gió, Feb 2026
            new() { Id = 35, SupplyInventoryId = 7,  Quantity = 500,   RemainingQuantity = 500,   ReceivedDate = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2029, 2, 12, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Purchase.ToString(), SourceId = 3, CreatedAt = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc) },

            // -- Donation import (Mar 2026) ----------------------------------
            // Log 44: mì tôm, Mar 2026
            new() { Id = 36, SupplyInventoryId = 1,  Quantity = 10000, RemainingQuantity = 10000, ReceivedDate = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 3, 2, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc) },

            // -- Closure test depots -----------------------------------------
            new() { Id = 62, SupplyInventoryId = 289, Quantity = 1200, RemainingQuantity = 1200, ReceivedDate = new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 2, 18, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 6, CreatedAt = new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc) },
            new() { Id = 63, SupplyInventoryId = 290, Quantity = 800,  RemainingQuantity = 800,  ReceivedDate = new DateTime(2026, 2, 18, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 8, 18, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, CreatedAt = new DateTime(2026, 2, 18, 9, 0, 0, DateTimeKind.Utc) },
            new() { Id = 64, SupplyInventoryId = 291, Quantity = 300,  RemainingQuantity = 300,  ReceivedDate = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 6, CreatedAt = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc) },
            new() { Id = 65, SupplyInventoryId = 292, Quantity = 1500, RemainingQuantity = 1500, ReceivedDate = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, CreatedAt = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc) },
            new() { Id = 66, SupplyInventoryId = 293, Quantity = 1000, RemainingQuantity = 1000, ReceivedDate = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 9, 5, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, CreatedAt = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc) },
            new() { Id = 67, SupplyInventoryId = 294, Quantity = 400,  RemainingQuantity = 400,  ReceivedDate = new DateTime(2026, 3, 6, 8, 30, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), SourceType = InventorySourceType.Donation.ToString(), SourceId = 4, CreatedAt = new DateTime(2026, 3, 6, 8, 30, 0, DateTimeKind.Utc) },
        };

        modelBuilder.Entity<SupplyInventoryLot>().HasData(lots.ToArray());
    }

    private static void SeedInventoryLogs(ModelBuilder modelBuilder)
    {
        // DSI layout: 44 consumable items per depot, sequential IDs
        // Depot 1: DSI 1-44, Depot 2: DSI 45-88, Depot 3: DSI 89-132, Depot 4: DSI 133-176
        // Index 0=mì tôm(1), 1=nu?c(2), 2=thu?c(3), 3=bang VS(5), 4=s?a b?t(7), 5=luong khô(8), 6=d?u gió(9), 7=vitamin(10)

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
            // -- Nh?p kho ban d?u ----------------------------------------------
            // Depot 1 (Hu?) - DSI 1-8
            new InventoryLog { Id = 1,  DepotSupplyInventoryId = 1,  SupplyInventoryLotId = 1,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 50000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  PerformedBy = mgr1, Note = "Nh?p mì tôm kho Hu? t? H?i CTÐ TT-Hu?",        ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 2,  DepotSupplyInventoryId = 2,  SupplyInventoryLotId = 2,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 40000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 1,  PerformedBy = mgr1, Note = "Nh?p nu?c u?ng kho Hu?",                        ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 3,  DepotSupplyInventoryId = 3,  SupplyInventoryLotId = 3,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 80000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr1, Note = "Nh?p thu?c Paracetamol kho Hu?",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 4,  DepotSupplyInventoryId = 4,  SupplyInventoryLotId = 4,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 15000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr1, Note = "Nh?p bang v? sinh kho Hu?",                     ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 5,  DepotSupplyInventoryId = 5,  SupplyInventoryLotId = 5,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 8000,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr1, Note = "Nh?p s?a b?t tr? em kho Hu?",                   ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 6,  DepotSupplyInventoryId = 6,  SupplyInventoryLotId = 6,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr1, Note = "Nh?p luong khô kho Hu?",                        ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 7,  DepotSupplyInventoryId = 7,  SupplyInventoryLotId = 7,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 5000,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr1, Note = "Nh?p d?u gió kho Hu?",                          ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 8,  DepotSupplyInventoryId = 8,  SupplyInventoryLotId = 8,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 20000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr1, Note = "Nh?p Vitamin t?ng h?p kho Hu?",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // Depot 2 (Ðà N?ng) - DSI 45-50
            new InventoryLog { Id = 9,  DepotSupplyInventoryId = 45, SupplyInventoryLotId = 9,  ActionType = InventoryActionType.Import.ToString(), QuantityChange = 40000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nh?p mì tôm kho Ðà N?ng t? Qu? T?m Lòng Vàng", ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 10, DepotSupplyInventoryId = 46, SupplyInventoryLotId = 10, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 32000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nh?p nu?c u?ng kho Ðà N?ng",                    ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 11, DepotSupplyInventoryId = 47, SupplyInventoryLotId = 11, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 64000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3,  PerformedBy = mgr2, Note = "Nh?p thu?c h? s?t kho Ðà N?ng",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 12, DepotSupplyInventoryId = 48, SupplyInventoryLotId = 12, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 12000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr2, Note = "Nh?p bang v? sinh kho Ðà N?ng",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 13, DepotSupplyInventoryId = 49, SupplyInventoryLotId = 13, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 6400,   SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr2, Note = "Nh?p s?a b?t kho Ðà N?ng",                     ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 14, DepotSupplyInventoryId = 50, SupplyInventoryLotId = 14, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 24000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr2, Note = "Nh?p luong khô kho Ðà N?ng t? Ban PCTT",       ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },

            // Depot 3 (Hà Tinh) - DSI 89-96
            new InventoryLog { Id = 15, DepotSupplyInventoryId = 89,  SupplyInventoryLotId = 15, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nh?p mì tôm kho Hà Tinh t? H?i LHPN",          ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 16, DepotSupplyInventoryId = 90,  SupplyInventoryLotId = 16, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 24000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 2,  PerformedBy = mgr3, Note = "Nh?p nu?c u?ng kho Hà Tinh t? MTTQ QB",        ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 17, DepotSupplyInventoryId = 91,  SupplyInventoryLotId = 17, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 48000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nh?p thu?c kho Hà Tinh",                       ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 18, DepotSupplyInventoryId = 92,  SupplyInventoryLotId = 18, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 9000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr3, Note = "Nh?p bang v? sinh kho Hà Tinh",                ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 19, DepotSupplyInventoryId = 93,  SupplyInventoryLotId = 19, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 4800,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr3, Note = "Nh?p s?a b?t tr? em kho Hà Tinh",              ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 20, DepotSupplyInventoryId = 95,  SupplyInventoryLotId = 20, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 3000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr3, Note = "Nh?p d?u gió kho Hà Tinh",                     ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 21, DepotSupplyInventoryId = 96,  SupplyInventoryLotId = 21, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 12000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr3, Note = "Nh?p Vitamin kho Hà Tinh",                     ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // Depot 4 (MTTQVN) - DSI 133-140
            new InventoryLog { Id = 22, DepotSupplyInventoryId = 133, SupplyInventoryLotId = 22, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 45000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nh?p mì tôm kho trung uong t? Ban PCTT",       ReceivedDate = now, ExpiredDate = exp12, CreatedAt = now },
            new InventoryLog { Id = 23, DepotSupplyInventoryId = 134, SupplyInventoryLotId = 23, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 36000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nh?p nu?c u?ng kho trung uong",                 ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 24, DepotSupplyInventoryId = 135, SupplyInventoryLotId = 24, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 72000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 7,  PerformedBy = mgr4, Note = "Nh?p thu?c kho trung uong t? CTÐ Qu?ng Ngãi",  ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 25, DepotSupplyInventoryId = 136, SupplyInventoryLotId = 25, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 13500, SourceType = InventorySourceType.Donation.ToString(), SourceId = 5,  PerformedBy = mgr4, Note = "Nh?p bang v? sinh kho trung uong",              ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },
            new InventoryLog { Id = 26, DepotSupplyInventoryId = 137, SupplyInventoryLotId = 26, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 7200,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr4, Note = "Nh?p s?a b?t kho trung uong",                  ReceivedDate = now, ExpiredDate = exp06, CreatedAt = now },
            new InventoryLog { Id = 27, DepotSupplyInventoryId = 138, SupplyInventoryLotId = 27, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 27000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 8,  PerformedBy = mgr4, Note = "Nh?p luong khô kho trung uong",                 ReceivedDate = now, ExpiredDate = exp24, CreatedAt = now },
            new InventoryLog { Id = 28, DepotSupplyInventoryId = 139, SupplyInventoryLotId = 28, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 4500,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 9,  PerformedBy = mgr4, Note = "Nh?p d?u gió kho trung uong",                   ReceivedDate = now, ExpiredDate = exp36, CreatedAt = now },
            new InventoryLog { Id = 29, DepotSupplyInventoryId = 140, SupplyInventoryLotId = 29, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 18000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 10, PerformedBy = mgr4, Note = "Nh?p Vitamin kho trung uong",                   ReceivedDate = now, ExpiredDate = exp18, CreatedAt = now },

            // -- M?u da d?ng các lo?i hành d?ng (không có ReceivedDate/ExpiredDate) --
            new InventoryLog { Id = 30, DepotSupplyInventoryId = 1,  ActionType = InventoryActionType.Export.ToString(),      QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),    MissionId = 1, PerformedBy = mgr1, Note = "Xu?t mì tôm cho nhi?m v? c?u h? lu l?t",              CreatedAt = now.AddHours(1) },
            new InventoryLog { Id = 31, DepotSupplyInventoryId = 45, ActionType = InventoryActionType.TransferOut.ToString(), QuantityChange = 2000,  SourceType = InventorySourceType.Transfer.ToString(),   SourceId = 1,  PerformedBy = mgr2, Note = "Chuy?n mì tôm t? Ðà N?ng sang kho Hu?",               CreatedAt = now.AddHours(2) },
            new InventoryLog { Id = 32, DepotSupplyInventoryId = 3,  ActionType = InventoryActionType.Adjust.ToString(),      QuantityChange = -1000, SourceType = InventorySourceType.Adjustment.ToString(),                PerformedBy = mgr1, Note = "Ði?u ch?nh s? lu?ng thu?c do h?t h?n",                CreatedAt = now.AddHours(3) },

            // -- Giao d?ch mua s?m (VAT) -------------------------------------
            // Jan 2025
            new InventoryLog { Id = 33, DepotSupplyInventoryId = 1,  VatInvoiceId = 1, SupplyInventoryLotId = 30, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 20000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nh?p mì tôm theo hóa don VAT Q1/2025",                  ReceivedDate = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 34, DepotSupplyInventoryId = 2,  VatInvoiceId = 1, SupplyInventoryLotId = 31, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 15000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nh?p nu?c tinh khi?t theo hóa don VAT Q1/2025",         ReceivedDate = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),  CreatedAt = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 35, DepotSupplyInventoryId = 1,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Xu?t mì tôm ph?c v? nhi?m v? c?u h? lu l?t",         CreatedAt = new DateTime(2025, 1, 15, 6, 30, 0, DateTimeKind.Utc) },

            // Jun 2025
            new InventoryLog { Id = 36, DepotSupplyInventoryId = 3,                    SupplyInventoryLotId = 32, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 1, PerformedBy = mgr1, Note = "Nh?n thu?c t? H?i Ch? Th?p Ð? Hu? d?t 2",             ReceivedDate = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc),  ExpiredDate = new DateTime(2027, 6, 5, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 37, DepotSupplyInventoryId = 4,                    ActionType = InventoryActionType.Adjust.ToString(), QuantityChange = -500,  SourceType = InventorySourceType.Adjustment.ToString(),             PerformedBy = mgr1, Note = "Ði?u ch?nh gi?m bang v? sinh do h?t h?n s? d?ng",      CreatedAt = new DateTime(2025, 6, 20, 10, 0, 0, DateTimeKind.Utc) },

            // Oct 2025
            new InventoryLog { Id = 38, DepotSupplyInventoryId = 5,                    SupplyInventoryLotId = 33, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 1000,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Nh?n s?a b?t t? MTTQ Qu?ng Bình h? tr?",              ReceivedDate = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 39, DepotSupplyInventoryId = 2,                    ActionType = InventoryActionType.TransferOut.ToString(), QuantityChange = 5000, SourceType = InventorySourceType.Transfer.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Chuy?n nu?c u?ng sang kho Ðà N?ng h? tr? bão s? 4", CreatedAt = new DateTime(2025, 10, 10, 6, 0, 0, DateTimeKind.Utc) },

            // Jan 2026
            new InventoryLog { Id = 40, DepotSupplyInventoryId = 3,  VatInvoiceId = 2, SupplyInventoryLotId = 34, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 30000, SourceType = InventorySourceType.Purchase.ToString(), SourceId = 2, PerformedBy = mgr1, Note = "Nh?p thu?c Paracetamol theo hóa don VAT d?u nam 2026", ReceivedDate = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc),   ExpiredDate = new DateTime(2028, 1, 8, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 41, DepotSupplyInventoryId = 4,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 200,   SourceType = InventorySourceType.Mission.ToString(),  MissionId = 2, PerformedBy = mgr1, Note = "Xu?t bang v? sinh cho d?i c?u h? phân ph?i vùng lu",  CreatedAt = new DateTime(2026, 1, 20, 9, 30, 0, DateTimeKind.Utc) },

            // Feb 2026
            new InventoryLog { Id = 42, DepotSupplyInventoryId = 7,  VatInvoiceId = 3, SupplyInventoryLotId = 35, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 500,   SourceType = InventorySourceType.Purchase.ToString(), SourceId = 3, PerformedBy = mgr1, Note = "Nh?p d?u gió theo hóa don VAT T2/2026",                ReceivedDate = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2029, 2, 12, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 43, DepotSupplyInventoryId = 6,                    ActionType = InventoryActionType.Return.ToString(), QuantityChange = 100,   SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Hoàn tr? luong khô sau khi k?t thúc nhi?m v? c?u h?", CreatedAt = new DateTime(2026, 2, 25, 14, 0, 0, DateTimeKind.Utc) },

            // Mar 2026
            new InventoryLog { Id = 44, DepotSupplyInventoryId = 1,                    SupplyInventoryLotId = 36, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 10000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, PerformedBy = mgr1, Note = "Ti?p nh?n mì tôm t? Qu? T?m Lòng Vàng Ðà N?ng",      ReceivedDate = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc),  ExpiredDate = new DateTime(2027, 3, 2, 0, 0, 0, DateTimeKind.Utc),   CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 45, DepotSupplyInventoryId = 3,                    ActionType = InventoryActionType.Export.ToString(), QuantityChange = 5000,  SourceType = InventorySourceType.Mission.ToString(),  MissionId = 1, PerformedBy = mgr1, Note = "Xu?t thu?c h? s?t c?p phát cho vùng thiên tai",      CreatedAt = new DateTime(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 46, DepotSupplyInventoryId = 2,                    ActionType = InventoryActionType.Adjust.ToString(), QuantityChange = -2000, SourceType = InventorySourceType.Adjustment.ToString(),             PerformedBy = mgr1, Note = "Ði?u ch?nh t?n kho nu?c sau ki?m kê d?nh k? quý I/2026", CreatedAt = new DateTime(2026, 3, 15, 16, 0, 0, DateTimeKind.Utc) },

            // -- L?ch s? xu?t / tr? cho Activity 6 + 9 (Mission 4, kho Hu? - consumable only) ---------------
            // Xu?t khi manager xác nh?n COLLECT_SUPPLIES Activity 6
            new InventoryLog { Id = 47, DepotSupplyInventoryId = 1, ActionType = InventoryActionType.Export.ToString(),  QuantityChange = 120, SourceType = InventorySourceType.Mission.ToString(), MissionId = 4, PerformedBy = mgr1, Note = "Xu?t mì tôm cho d?i v?n chuy?n Mission 4 (Activity 6)",             CreatedAt = new DateTime(2026, 3, 5, 7, 55, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 48, DepotSupplyInventoryId = 2, ActionType = InventoryActionType.Export.ToString(),  QuantityChange = 240, SourceType = InventorySourceType.Mission.ToString(), MissionId = 4, PerformedBy = mgr1, Note = "Xu?t nu?c tinh khi?t cho d?i v?n chuy?n Mission 4 (Activity 6)",       CreatedAt = new DateTime(2026, 3, 5, 7, 55, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 49, DepotSupplyInventoryId = 3, ActionType = InventoryActionType.Export.ToString(),  QuantityChange = 300, SourceType = InventorySourceType.Mission.ToString(), MissionId = 4, PerformedBy = mgr1, Note = "Xu?t thu?c h? s?t cho d?i v?n chuy?n Mission 4 (Activity 6)",          CreatedAt = new DateTime(2026, 3, 5, 7, 55, 0, DateTimeKind.Utc) },
            // Nh?n l?i khi manager xác nh?n RETURN_SUPPLIES Activity 9
            new InventoryLog { Id = 50, DepotSupplyInventoryId = 1, ActionType = InventoryActionType.Return.ToString(),  QuantityChange = 50,  SourceType = InventorySourceType.Mission.ToString(), MissionId = 4, PerformedBy = mgr1, Note = "Nh?n l?i mì tôm du th?a t? d?i v?n chuy?n Mission 4 (Activity 9)",      CreatedAt = new DateTime(2026, 3, 5, 11, 30, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 51, DepotSupplyInventoryId = 3, ActionType = InventoryActionType.Return.ToString(),  QuantityChange = 100, SourceType = InventorySourceType.Mission.ToString(), MissionId = 4, PerformedBy = mgr1, Note = "Nh?n l?i thu?c h? s?t du th?a t? d?i v?n chuy?n Mission 4 (Activity 9)", CreatedAt = new DateTime(2026, 3, 5, 11, 30, 0, DateTimeKind.Utc) },

            // -- L?ch s? xu?t / tr? cho Activity 10 + 11 (Mission 6, kho Hu? - consumable + reusable) --------
            // Xu?t consumable khi manager xác nh?n COLLECT_SUPPLIES Activity 10
            new InventoryLog { Id = 52, DepotSupplyInventoryId = 1, ActionType = InventoryActionType.Export.ToString(), QuantityChange = 100, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Xu?t mì tôm cho d?i v?n chuy?n Mission 6 (Activity 10)",             CreatedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 53, DepotSupplyInventoryId = 5, ActionType = InventoryActionType.Export.ToString(), QuantityChange = 50,  SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Xu?t chan ?m cho d?i v?n chuy?n Mission 6 (Activity 10)",           CreatedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc) },
            // Xu?t reusable (áo phao c?u sinh) - 1 log row m?i don v?
            new InventoryLog { Id = 54, ReusableItemId = 1, ActionType = InventoryActionType.Export.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Xu?t áo phao D1-R004-001 cho d?i v?n chuy?n Mission 6 (Activity 10)", CreatedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 55, ReusableItemId = 2, ActionType = InventoryActionType.Export.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Xu?t áo phao D1-R004-002 cho d?i v?n chuy?n Mission 6 (Activity 10)", CreatedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 56, ReusableItemId = 3, ActionType = InventoryActionType.Export.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Xu?t áo phao D1-R004-003 cho d?i v?n chuy?n Mission 6 (Activity 10)", CreatedAt = new DateTime(2026, 3, 8, 7, 55, 0, DateTimeKind.Utc) },
            // Nh?n l?i consumable du th?a khi manager xác nh?n RETURN_SUPPLIES Activity 11
            new InventoryLog { Id = 57, DepotSupplyInventoryId = 1, ActionType = InventoryActionType.Return.ToString(), QuantityChange = 30, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Nh?n l?i mì tôm du th?a t? d?i v?n chuy?n Mission 6 (Activity 11)",   CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 58, DepotSupplyInventoryId = 5, ActionType = InventoryActionType.Return.ToString(), QuantityChange = 8,  SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Nh?n l?i chan ?m du th?a t? d?i v?n chuy?n Mission 6 (Activity 11)", CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc) },
            // Nh?n l?i reusable (áo phao c?u sinh) - 1 log row m?i don v?
            new InventoryLog { Id = 59, ReusableItemId = 1, ActionType = InventoryActionType.Return.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Nh?n l?i áo phao D1-R004-001 t? d?i v?n chuy?n Mission 6 (Activity 11)", CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 60, ReusableItemId = 2, ActionType = InventoryActionType.Return.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Nh?n l?i áo phao D1-R004-002 t? d?i v?n chuy?n Mission 6 (Activity 11)", CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 61, ReusableItemId = 3, ActionType = InventoryActionType.Return.ToString(), QuantityChange = 1, SourceType = InventorySourceType.Mission.ToString(), MissionId = 6, PerformedBy = mgr1, Note = "Nh?n l?i áo phao D1-R004-003 t? d?i v?n chuy?n Mission 6 (Activity 11)", CreatedAt = new DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc) },

            // -- Seed riêng cho test dóng kho depot 5/6 ---------------------
            new InventoryLog { Id = 62, DepotSupplyInventoryId = 289, SupplyInventoryLotId = 62, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 1200, SourceType = InventorySourceType.Donation.ToString(), SourceId = 6, PerformedBy = SeedConstants.Manager5UserId, Note = "Nh?p mì tôm kho Thang Bình d? test x? lý dóng kho bên ngoài", ReceivedDate = new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 2, 18, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 63, DepotSupplyInventoryId = 290, SupplyInventoryLotId = 63, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 800,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 3, PerformedBy = SeedConstants.Manager5UserId, Note = "Nh?p nu?c tinh khi?t kho Thang Bình d? test dóng kho", ReceivedDate = new DateTime(2026, 2, 18, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 8, 18, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 2, 18, 9, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 64, DepotSupplyInventoryId = 291, SupplyInventoryLotId = 64, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 300,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 6, PerformedBy = SeedConstants.Manager5UserId, Note = "Nh?p áo mua ngu?i l?n kho Thang Bình d? test x? lý bên ngoài", ReceivedDate = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 2, 20, 8, 30, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 65, DepotSupplyInventoryId = 292, SupplyInventoryLotId = 65, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 1500, SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, PerformedBy = SeedConstants.Manager6UserId, Note = "Nh?p mì tôm kho Qu?ng Ninh d? test chuy?n kho khi dóng kho", ReceivedDate = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 66, DepotSupplyInventoryId = 293, SupplyInventoryLotId = 66, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 1000, SourceType = InventorySourceType.Donation.ToString(), SourceId = 2, PerformedBy = SeedConstants.Manager6UserId, Note = "Nh?p nu?c tinh khi?t kho Qu?ng Ninh d? test lu?ng chuy?n kho", ReceivedDate = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2027, 9, 5, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc) },
            new InventoryLog { Id = 67, DepotSupplyInventoryId = 294, SupplyInventoryLotId = 67, ActionType = InventoryActionType.Import.ToString(), QuantityChange = 400,  SourceType = InventorySourceType.Donation.ToString(), SourceId = 4, PerformedBy = SeedConstants.Manager6UserId, Note = "Nh?p áo mua ngu?i l?n kho Qu?ng Ninh d? test dóng kho chuy?n sang kho khác", ReceivedDate = new DateTime(2026, 3, 6, 8, 30, 0, DateTimeKind.Utc), ExpiredDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 3, 6, 8, 30, 0, DateTimeKind.Utc) }
        );
    }

    private static void SeedOrganizationReliefItems(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<OrganizationReliefItem>().HasData(
            new OrganizationReliefItem { Id = 1,  OrganizationId = 1,  ItemModelId = 1,  Quantity = 50000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "C?u tr? d?t 1",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 2,  OrganizationId = 2,  ItemModelId = 2,  Quantity = 40000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "C?u tr? d?t 1",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 3,  OrganizationId = 3,  ItemModelId = 3,  Quantity = 80000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "C?u tr? y t?",               CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 4,  OrganizationId = 4,  ItemModelId = 4,  Quantity = 100,   ReceivedDate = seedDate, ExpiredDate = null,                                                     Notes = "Trang thi?t b? T?nh doàn",       CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 5,  OrganizationId = 5,  ItemModelId = 5,  Quantity = 15000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Nhu y?u ph?m ph? n?",          CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 6,  OrganizationId = 6,  ItemModelId = 6,  Quantity = 200,   ReceivedDate = seedDate, ExpiredDate = null,                                                     Notes = "Áo l?nh mùa dông",              CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 7,  OrganizationId = 7,  ItemModelId = 7,  Quantity = 8000,  ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Dinh du?ng tr? em",           CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 8,  OrganizationId = 8,  ItemModelId = 8,  Quantity = 30000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "Luong khô kh?n c?p",           CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 9,  OrganizationId = 9,  ItemModelId = 9,  Quantity = 5000,  ReceivedDate = seedDate, ExpiredDate = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "Y t? ngu?i già",               CreatedAt = seedDate },
            new OrganizationReliefItem { Id = 10, OrganizationId = 10, ItemModelId = 10, Quantity = 20000, ReceivedDate = seedDate, ExpiredDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),  Notes = "B? sung Vitamin",             CreatedAt = seedDate }
        );
    }

    private static void SeedVatInvoices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoice>().HasData(
            new VatInvoice { Id = 1, InvoiceSerial = "AA", InvoiceNumber = "0001234", SupplierName = "Công ty TNHH Hùng Phúc",         SupplierTaxCode = "0301234567", InvoiceDate = new DateOnly(2025, 1, 10), TotalAmount = 145_000_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoice { Id = 2, InvoiceSerial = "AA", InvoiceNumber = "0001235", SupplierName = "Chu?i Siêu th? Bigmart Hu?",     SupplierTaxCode = "0305678901", InvoiceDate = new DateOnly(2026, 1,  8), TotalAmount =  60_000_000m, CreatedAt = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoice { Id = 3, InvoiceSerial = "BB", InvoiceNumber = "0002001", SupplierName = "Công ty Du?c ph?m Minh Châu",    SupplierTaxCode = "0302345678", InvoiceDate = new DateOnly(2026, 2, 12), TotalAmount =  75_000_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );
    }

    private static void SeedVatInvoiceItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VatInvoiceItem>().HasData(
            // Invoice 1 (Jan 2025): mì tôm + nu?c
            new VatInvoiceItem { Id = 1, VatInvoiceId = 1, ItemModelId = 1, Quantity = 20000, UnitPrice =   3_500m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoiceItem { Id = 2, VatInvoiceId = 1, ItemModelId = 2, Quantity = 15000, UnitPrice =   5_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 2 (Jan 2026): thu?c h? s?t
            new VatInvoiceItem { Id = 3, VatInvoiceId = 2, ItemModelId = 3, Quantity = 30000, UnitPrice =   2_000m, CreatedAt = new DateTime(2026, 1,  8, 0, 0, 0, DateTimeKind.Utc) },
            // Invoice 3 (Feb 2026): d?u gió
            new VatInvoiceItem { Id = 4, VatInvoiceId = 3, ItemModelId = 9, Quantity =  5000, UnitPrice =  15_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );
    }

    // -- Depot-to-depot supply requests ----------------------------------------
    private static void SeedDepotSupplyRequests(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<DepotSupplyRequest>().HasData(
            // Req 1 (COMPLETED - 2 bên dã xác nh?n): Kho 3 (Hà Tinh) xin t? Kho 1 (Hu?)
            new DepotSupplyRequest
            {
                Id = 1, RequestingDepotId = 3, SourceDepotId = 1,
                Note = "Thi?u luong th?c và nu?c u?ng c?u tr? kh?n c?p",
                PriorityLevel     = "High",
                SourceStatus      = SourceDepotStatus.Completed.ToString(),
                RequestingStatus  = RequestingDepotStatus.Received.ToString(),
                RequestedBy       = SeedConstants.Manager3UserId,
                CreatedAt         = now.AddDays(-28),
                AutoRejectAt      = now.AddDays(-28).AddHours(2),
                RespondedAt       = now.AddDays(-27),
                ShippedAt         = now.AddDays(-26),
                CompletedAt       = now.AddDays(-25)
            },
            // Req 2 (COMPLETED - 2 bên dã xác nh?n): Kho 2 (Ðà N?ng) xin t? Kho 1 (Hu?)
            new DepotSupplyRequest
            {
                Id = 2, RequestingDepotId = 2, SourceDepotId = 1,
                Note = "B? sung thu?c y t? cho kho Ðà N?ng",
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
            // Req 3 (COMPLETED - 2 bên dã xác nh?n): Kho 4 (HN) xin t? Kho 2 (Ðà N?ng)
            new DepotSupplyRequest
            {
                Id = 3, RequestingDepotId = 4, SourceDepotId = 2,
                Note = "B? sung nu?c u?ng cho kho trung uong",
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
            // Req 4 (COMPLETED - 2 bên dã xác nh?n): Kho 1 (Hu?) xin t? Kho 4 (HN)
            new DepotSupplyRequest
            {
                Id = 4, RequestingDepotId = 1, SourceDepotId = 4,
                Note = "B? sung chan ?m và thi?t b? su?i t? kho trung uong",
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
            // Req 5 (REJECTED): Kho 1 (Hu?) xin t? Kho 3 (Hà Tinh) - b? t? ch?i
            new DepotSupplyRequest
            {
                Id = 5, RequestingDepotId = 1, SourceDepotId = 3,
                Note = "C?n b? sung d?ng c? c?u h? kh?n c?p",
                PriorityLevel     = "High",
                SourceStatus      = SourceDepotStatus.Rejected.ToString(),
                RequestingStatus  = RequestingDepotStatus.Rejected.ToString(),
                RejectedReason    = "Kho Hà Tinh không d? t?n kho d? dáp ?ng",
                RequestedBy       = SeedConstants.ManagerUserId,
                CreatedAt         = now.AddDays(-30),
                AutoRejectAt      = now.AddDays(-30).AddHours(2),
                RespondedAt       = now.AddDays(-30).AddHours(1)
            },
            // Req 6 (COMPLETED - 2 bên dã xác nh?n): Kho 1 (Hu?) xin t? Kho 2 (Ðà N?ng)
            new DepotSupplyRequest
            {
                Id = 6, RequestingDepotId = 1, SourceDepotId = 2,
                Note = "B? sung thu?c y t? d? phòng cho mùa lu l?t",
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
            // Req 1 (COMPLETED): mì tôm + nu?c
            new DepotSupplyRequestItem { Id = 1, DepotSupplyRequestId = 1, ItemModelId = 1, Quantity = 6000 },
            new DepotSupplyRequestItem { Id = 2, DepotSupplyRequestId = 1, ItemModelId = 2, Quantity = 4000 },
            // Req 2 (COMPLETED): thu?c Paracetamol
            new DepotSupplyRequestItem { Id = 3, DepotSupplyRequestId = 2, ItemModelId = 3, Quantity = 5000 },
            // Req 3 (COMPLETED): nu?c tinh khi?t
            new DepotSupplyRequestItem { Id = 4, DepotSupplyRequestId = 3, ItemModelId = 2, Quantity = 8000 },
            // Req 4 (COMPLETED): chan ?m gi? nhi?t
            new DepotSupplyRequestItem { Id = 5, DepotSupplyRequestId = 4, ItemModelId = 6, Quantity = 200 },
            // Req 5 (REJECTED): d?ng c? c?u h?
            new DepotSupplyRequestItem { Id = 6, DepotSupplyRequestId = 5, ItemModelId = 5, Quantity = 50 },
            new DepotSupplyRequestItem { Id = 7, DepotSupplyRequestId = 5, ItemModelId = 4, Quantity = 100 },
            // Req 6 (COMPLETED): thu?c b? sung
            new DepotSupplyRequestItem { Id = 8, DepotSupplyRequestId = 6, ItemModelId = 3, Quantity = 3000 }
        );
    }
}

