using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private static readonly string[] DepotClosureTestDepotNames =
    [
        "Kho cứu trợ Đại học Phú Yên",
        "Ga đường sắt Sài Gòn"
    ];
    private static readonly string[] HueDepotExcludedItemNames =
    [
        "Pin dự phòng 10000mAh",
        "Bộ đèn pin đội đầu"
    ];

    private async Task SeedLogisticsCatalogAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var categoryDefs = new[]
        {
            ("Food",            "Thực phẩm",        "Lương thực, đồ ăn khô, thực phẩm ăn liền"),
            ("Water",           "Nước uống",        "Nước sạch, nước đóng chai, điện giải"),
            ("Medical",         "Y tế",             "Thuốc men, vật tư y tế, bộ sơ cứu"),
            ("Hygiene",         "Vệ sinh cá nhân",  "Khăn giấy, xà phòng, băng vệ sinh, tã"),
            ("Clothing",        "Quần áo",           "Quần áo sạch, áo mưa, đồ giữ ấm cơ bản"),
            ("Shelter",         "Nơi trú ẩn",        "Lều bạt, túi ngủ, vật dụng che chắn"),
            ("RepairTools",     "Công cụ sửa chữa", "Búa, đinh, cưa, dụng cụ khắc phục khẩn cấp"),
            ("RescueEquipment", "Thiết bị cứu hộ",  "Áo phao, xuồng, dây cứu sinh, bộ đàm"),
            ("Heating",         "Sưởi ấm",           "Chăn, bếp dã chiến, vật dụng giữ nhiệt"),
            ("Vehicle",         "Phương tiện",       "Xe tải, xe cứu thương, ca nô, xe địa hình"),
            ("Others",          "Khác",              "Thiết bị hỗ trợ, tín hiệu, chiếu sáng, ghi nhận hiện trường")
        };

        foreach (var (code, name, description) in categoryDefs)
        {
            seed.Categories.Add(new Category
            {
                Code = code,
                Name = name,
                Description = description,
                Quantity = 0,
                CreatedAt = seed.StartUtc,
                UpdatedAt = seed.AnchorUtc,
                CreatedBy = seed.Admins[0].Id,
                UpdatedBy = seed.Admins[0].Id
            });
        }

        _db.Categories.AddRange(seed.Categories);
        await _db.SaveChangesAsync(cancellationToken);

        var targetGroupsByName = (await _db.TargetGroups.OrderBy(t => t.Id).ToListAsync(cancellationToken))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var baseItems = BaseItemModels();
        var imageIds = ReliefItemImageIdsInSeedOrder();
        if (imageIds.Count != baseItems.Count)
        {
            throw new InvalidOperationException("Relief item image id mapping must match the seeded item model count.");
        }

        for (var i = 0; i < baseItems.Count; i++)
        {
            var template = baseItems[i];
            var category = seed.Categories.Single(c => c.Code == template.CategoryCode);
            var item = new ItemModel
            {
                CategoryId = category.Id,
                Name = template.Name,
                Description = template.Description,
                Unit = template.Unit,
                ItemType = template.ItemType,
                VolumePerUnit = template.Volume,
                WeightPerUnit = template.Weight,
                ImageUrl = GetReliefItemImageUrl(imageIds[i]) ?? $"https://cdn.resq.vn/items/{Slug(template.Name)}.jpg",
                CreatedAt = seed.StartUtc.AddDays(15 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-(i % 60)),
                UpdatedBy = seed.Managers[i % seed.Managers.Count].Id
            };

            foreach (var targetGroupName in TargetGroupNamesFor(template))
            {
                if (targetGroupsByName.TryGetValue(targetGroupName, out var targetGroup))
                {
                    item.TargetGroups.Add(targetGroup);
                }
            }

            seed.ItemModels.Add(item);
        }

        _db.ItemModels.AddRange(seed.ItemModels);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDepotsAndInventoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var depotDefs = new[]
        {
            ("Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", "46 Đống Đa, TP. Huế, Thừa Thiên Huế", 16.463040, 107.594184, "Available", 1_100_000m, 440_000m, 80_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg"),
            ("Ủy ban MTTQVN TP Đà Nẵng", "270 Trưng Nữ Vương, Hải Châu, Đà Nẵng", 16.080298466000496, 108.22283205420794, "Available", 1_000_000m, 480_000m, 60_000_000m, 10_000_000m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy Ban MTTQ Tỉnh Hà Tĩnh", "72 Phan Đình Phùng, TP. Hà Tĩnh, Hà Tĩnh", 18.349622333272194, 105.90102499916586, "Available", 600_000m, 260_000m, 40_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg"),
            ("Ủy ban MTTQVN Việt Nam", "46 Tràng Thi, Hoàn Kiếm, Hà Nội", 21.027819, 105.842191, "Available", 1_400_000m, 650_000m, 100_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Thăng Bình", "282 Tiểu La, thị trấn Hà Lam, huyện Thăng Bình, Quảng Nam", 15.6949, 108.4587, "Available", 250_000m, 120_000m, 12_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Quảng Ninh", "TT. Quán Hàu, huyện Quảng Ninh, Quảng Bình", 17.4619, 106.6175, "Available", 280_000m, 140_000m, 14_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Tỉnh Nghệ An", "1 Phan Đăng Lưu, TP. Vinh, Nghệ An", 18.6732581, 105.6936046, "Available", 300_000m, 150_000m, 5_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            (DepotClosureTestDepotNames[0], "Đại học Phú Yên, TP. Tuy Hòa, Phú Yên", 13.106332, 109.306890, "Available", 520_000m, 210_000m, 18_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            (DepotClosureTestDepotNames[1], "Ga đường sắt Sài Gòn, Quận 3, TP. Hồ Chí Minh", 10.782103, 106.678803, "Available", 900_000m, 360_000m, 30_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg")
        };
        for (var i = 0; i < depotDefs.Length; i++)
        {
            var (name, address, lat, lon, status, capacity, weightCapacity, advanceLimit, outstandingAdvanceAmount, imageUrl) = depotDefs[i];
            seed.Depots.Add(new Depot
            {
                Name = name,
                Address = address,
                Location = Point(lon, lat),
                Status = status,
                Capacity = capacity,
                CurrentUtilization = 0m,
                WeightCapacity = weightCapacity,
                CurrentWeightUtilization = 0m,
                AdvanceLimit = advanceLimit,
                OutstandingAdvanceAmount = outstandingAdvanceAmount,
                LastUpdatedAt = seed.AnchorUtc.AddDays(-i),
                CreatedBy = seed.Admins[0].Id,
                LastUpdatedBy = seed.Managers[i % seed.Managers.Count].Id,
                LastStatusChangedBy = seed.Managers[i % seed.Managers.Count].Id,
                ImageUrl = imageUrl
            });
        }

        _db.Depots.AddRange(seed.Depots);
        await _db.SaveChangesAsync(cancellationToken);

        var depotManagers = new List<DepotManager>();
        for (var i = 0; i < seed.Depots.Count; i++)
        {
            depotManagers.Add(new DepotManager
            {
                DepotId = seed.Depots[i].Id,
                UserId = seed.Managers[i].Id,
                AssignedAt = seed.StartUtc.AddDays(30 + i),
                AssignedBy = seed.Admins[0].Id
            });
        }
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[0].Id, UserId = seed.Managers[6].Id, AssignedAt = seed.StartUtc.AddDays(1), UnassignedAt = seed.StartUtc.AddDays(80), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[3].Id, UserId = seed.Managers[7].Id, AssignedAt = seed.StartUtc.AddDays(10), UnassignedAt = seed.StartUtc.AddDays(95), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        _db.DepotManagers.AddRange(depotManagers);

        var organizations = new List<Organization>();
        for (var i = 0; i < 14; i++)
        {
            organizations.Add(new Organization
            {
                Name = OrganizationName(i),
                Phone = Phone(7, i + 1),
                Email = $"contact{i + 1:00}@cuutro-mientrung.vn",
                IsActive = i % 11 != 0,
                CreatedAt = seed.StartUtc.AddDays(40 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i)
            });
        }
        _db.Organizations.AddRange(organizations);
        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < 90; i++)
        {
            var item = seed.ItemModels[i % seed.ItemModels.Count];
            _db.OrganizationReliefItems.Add(new OrganizationReliefItem
            {
                OrganizationId = organizations[i % organizations.Count].Id,
                ItemModelId = item.Id,
                Quantity = 80 + (i % 12) * 30,
                ReceivedDate = seed.StartUtc.AddDays(100 + i * 5),
                ExpiredDate = item.ItemType == "Consumable" ? seed.AnchorUtc.AddDays(120 + i % 120) : null,
                Notes = "Ủng hộ đợt mưa lũ miền Trung",
                ReceivedBy = seed.Managers[i % seed.Managers.Count].Id,
                CreatedAt = seed.StartUtc.AddDays(100 + i * 5)
            });
        }

        var consumableModels = seed.ItemModels
            .Where(item => string.Equals(item.ItemType, nameof(ItemType.Consumable), StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Id)
            .ToList();
        var inventoryTarget = Math.Min(620, seed.Depots.Count * consumableModels.Count);
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var itemCount = Math.Min(consumableModels.Count, 69 + (depotIndex < 2 ? 1 : 0));
            for (var itemOffset = 0; itemOffset < itemCount && seed.Inventories.Count < inventoryTarget; itemOffset++)
            {
                var item = consumableModels[(depotIndex * 11 + itemOffset) % consumableModels.Count];
                var quantity = 160 + (itemOffset % 30) * 20;
                seed.Inventories.Add(new SupplyInventory
                {
                    DepotId = seed.Depots[depotIndex].Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    MissionReservedQuantity = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt = seed.AnchorUtc.AddDays(-itemOffset % 90),
                    IsDeleted = false
                });
            }
        }

        var lifeJacketModel = seed.ItemModels.Single(m => m.Name == "Áo phao cứu sinh");
        var blanketModel = seed.ItemModels.Single(m => m.Name == "Chăn ấm giữ nhiệt");
        EnsureEssentialDepotStock(seed, blanketModel);
        EnsureNewMedicinesInHueDepot(seed);
        EnsureClosureTestDepotsFullInventory(seed);
        ExcludeHueDepotItems(seed);
        EnsureHueDepotDepletedBabyFormula(seed);

        _db.SupplyInventories.AddRange(seed.Inventories);
        await _db.SaveChangesAsync(cancellationToken);

        var consumableInventories = seed.Inventories
            .Where(i => seed.ItemModels.First(m => m.Id == i.ItemModelId).ItemType == "Consumable")
            .ToList();
        foreach (var inventory in consumableInventories)
        {
            var received = seed.AnchorUtc.AddDays(-30 - seed.Lots.Count % 300);
            var quantity = Math.Max(0, inventory.Quantity ?? 0);
            var sourceType = seed.Lots.Count % 3 == 0 ? "Purchase" : "Donation";
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = quantity,
                RemainingQuantity = quantity,
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(6 + seed.Lots.Count % 18),
                SourceType = sourceType,
                SourceId = seed.Lots.Count + 1,
                CreatedAt = received
            });
        }
        EnsureEssentialBlanketLots(seed, blanketModel);
        EnsureHueDepotExpiringConsumableLots(seed);
        EnsureClosureTestDepotsConsumableLots(seed);
        _db.SupplyInventoryLots.AddRange(seed.Lots);

        var reusableModels = seed.ItemModels.Where(m => m.ItemType == "Reusable").ToList();
        for (var i = 0; i < 220; i++)
        {
            var item = reusableModels[i % reusableModels.Count];
            var depot = seed.Depots[i % seed.Depots.Count];
            seed.ReusableItems.Add(new ReusableItem
            {
                DepotId = depot.Id,
                ItemModelId = item.Id,
                SerialNumber = $"{Slug(item.Name ?? "item").ToUpperInvariant()}-{Area(i).Code}-{i + 1:00000}",
                Status = i % 17 == 0 ? "Maintenance" : "Available",
                Condition = i % 11 == 0 ? "Fair" : i % 29 == 0 ? "Poor" : "Good",
                Note = i % 17 == 0 ? "Đang kiểm tra sau nhiệm vụ" : null,
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i % 90),
                IsDeleted = false
            });
        }
        EnsureLifeJacketReusableUnits(seed, lifeJacketModel);
        EnsureClosureTestDepotsReusableUnits(seed);
        EnsureManagerReturnFixtureReusableUnits(seed);
        ExcludeHueDepotReusableUnits(seed);
        RecomputeSeedDepotUtilization(seed);
        _db.ReusableItems.AddRange(seed.ReusableItems);

        await SeedVatInvoicesAsync(seed);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedVatInvoicesAsync(DemoSeedContext seed)
    {
        var invoices = new List<VatInvoice>();
        for (var i = 0; i < 50; i++)
        {
            var date = DateOnly.FromDateTime(seed.StartUtc.AddDays(180 + i * 17));
            invoices.Add(new VatInvoice
            {
                InvoiceSerial = $"AA/{date.Year % 100:00}E",
                InvoiceNumber = $"{1800 + i:0000000}",
                SupplierName = SupplierName(i),
                SupplierTaxCode = $"330{1234560 + i}",
                InvoiceDate = date,
                TotalAmount = 8_500_000 + i * 420_000,
                FileUrl = $"https://cdn.resq.vn/vat/{date.Year}-{i + 1:000}.pdf",
                CreatedAt = VnToUtc(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))))
            });
        }

        _db.VatInvoices.AddRange(invoices);
        await _db.SaveChangesAsync();

        foreach (var invoice in invoices)
        {
            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(invoice.Id * 5 + j) % seed.ItemModels.Count];
                var quantity = 20 + (invoice.Id + j) % 80;
                var price = item.ItemType == "Reusable" ? 450_000 + j * 250_000 : 12_000 + j * 8_000;
                _db.VatInvoiceItems.Add(new VatInvoiceItem
                {
                    VatInvoiceId = invoice.Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    UnitPrice = price,
                    CreatedAt = invoice.CreatedAt
                });
            }
        }
    }


    private static Depot OperationalDepotForActivity(DemoSeedContext seed, int missionId, int step)
    {
        var operationalDepots = seed.Depots
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        return operationalDepots[(missionId + step) % operationalDepots.Count];
    }

    private static bool IsDepotClosureTestCandidate(Depot depot) =>
        DepotClosureTestDepotNames.Contains(depot.Name, StringComparer.Ordinal);

    private static IReadOnlyList<Depot> FindClosureTestDepots(DemoSeedContext seed) =>
        seed.Depots
            .Where(IsDepotClosureTestCandidate)
            .OrderBy(depot => depot.Id)
            .ToList();

    private static Depot? FindHueDepot(DemoSeedContext seed) =>
        seed.Depots.FirstOrDefault();

    private static HashSet<int> HueDepotExcludedItemModelIds(DemoSeedContext seed) =>
        seed.ItemModels
            .Where(item => item.Name != null
                && HueDepotExcludedItemNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet();

    private static void ExcludeHueDepotItems(DemoSeedContext seed)
    {
        var hueDepot = FindHueDepot(seed);
        if (hueDepot is null)
        {
            return;
        }

        var excludedItemModelIds = HueDepotExcludedItemModelIds(seed);
        if (excludedItemModelIds.Count == 0)
        {
            return;
        }

        seed.Inventories.RemoveAll(inventory =>
            inventory.DepotId == hueDepot.Id
            && inventory.ItemModelId.HasValue
            && excludedItemModelIds.Contains(inventory.ItemModelId.Value));
    }

    private static void ExcludeHueDepotReusableUnits(DemoSeedContext seed)
    {
        var hueDepot = FindHueDepot(seed);
        if (hueDepot is null)
        {
            return;
        }

        var excludedReusableModelIds = seed.ItemModels
            .Where(item => string.Equals(item.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Name != null
                && HueDepotExcludedItemNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet();
        if (excludedReusableModelIds.Count == 0)
        {
            return;
        }

        seed.ReusableItems.RemoveAll(item =>
            item.DepotId == hueDepot.Id
            && item.ItemModelId.HasValue
            && excludedReusableModelIds.Contains(item.ItemModelId.Value));
    }

    private static void EnsureEssentialDepotStock(DemoSeedContext seed, ItemModel blanketModel)
    {
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            EnsureDepotInventory(seed, depot.Id, blanketModel.Id, EssentialBlanketQuantity(depotIndex), depotIndex);
        }
    }

    private static void EnsureNewMedicinesInHueDepot(DemoSeedContext seed)
    {
        var hueDepot = FindHueDepot(seed);
        if (hueDepot is null)
            return;

        // 14 new medicines added for flood relief
        var newMedicineNames = new[]
        {
            "Thuốc tiêu chảy Loperamide",
            "Thuốc trị nhiễm khuẩn đường ruột Smecta",
            "Thuốc chống nôn Domperidone",
            "Thuốc cảm cúm tổng hợp Decolgen",
            "Thuốc ho Dextromethorphan",
            "Thuốc long đờm Acetylcysteine",
            "Thuốc chống dị ứng Loratadine",
            "Kem bôi ngoài da Hydrocortisone",
            "Thuốc chống nấm da Clotrimazole",
            "Thuốc giảm đau kháng viêm Ibuprofen",
            "Thuốc nhỏ mắt (viêm kết mạc)",
            "Thuốc nhỏ mũi (nghẹt mũi do lạnh)",
            "Vitamin C liều cao",
            "Thuốc chống say nước"
        };

        var quantities = new[] { 5000, 3000, 2000, 4000, 3000, 2500, 3500, 1500, 1500, 4000, 2000, 2000, 5000, 1000 };

        for (var i = 0; i < newMedicineNames.Length; i++)
        {
            var medicineName = newMedicineNames[i];
            var medicineModel = seed.ItemModels.FirstOrDefault(m => m.Name == medicineName);
            if (medicineModel is null)
                continue;

            EnsureDepotInventory(seed, hueDepot.Id, medicineModel.Id, quantities[i], 0);
        }
    }

    private static void EnsureDepotInventory(DemoSeedContext seed, int depotId, int itemModelId, int quantity, int depotIndex)
    {
        var itemModel = seed.ItemModels.FirstOrDefault(model => model.Id == itemModelId);
        if (!string.Equals(itemModel?.ItemType, nameof(ItemType.Consumable), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var inventory = seed.Inventories.FirstOrDefault(i => i.DepotId == depotId && i.ItemModelId == itemModelId);
        if (inventory is null)
        {
            seed.Inventories.Add(new SupplyInventory
            {
                DepotId = depotId,
                ItemModelId = itemModelId,
                Quantity = quantity,
                MissionReservedQuantity = 0,
                TransferReservedQuantity = 0,
                LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex),
                IsDeleted = false
            });
            return;
        }

        inventory.Quantity = quantity;
        inventory.MissionReservedQuantity = 0;
        inventory.TransferReservedQuantity = 0;
        inventory.LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex);
        inventory.IsDeleted = false;
    }

    private static void EnsureClosureTestDepotsFullInventory(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        foreach (var closureDepot in closureDepots)
        {
            foreach (var item in seed.ItemModels.OrderBy(model => model.Id))
            {
                if (!string.Equals(item.ItemType, nameof(ItemType.Consumable), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var inventory = seed.Inventories.FirstOrDefault(i => i.DepotId == closureDepot.Id && i.ItemModelId == item.Id);
                if (inventory is null)
                {
                    var quantity = ClosureTestDepotQuantity(item);
                    seed.Inventories.Add(new SupplyInventory
                    {
                        DepotId = closureDepot.Id,
                        ItemModelId = item.Id,
                        Quantity = quantity,
                        MissionReservedQuantity = 0,
                        TransferReservedQuantity = 0,
                        LastStockedAt = seed.AnchorUtc.AddDays(-(18 + item.Id % 40)),
                        IsDeleted = false
                    });
                    continue;
                }

                inventory.MissionReservedQuantity = 0;
                inventory.TransferReservedQuantity = 0;
                inventory.LastStockedAt = seed.AnchorUtc.AddDays(-(18 + item.Id % 40));
                inventory.IsDeleted = false;
            }
        }
    }

    private static void EnsureHueDepotDepletedBabyFormula(DemoSeedContext seed)
    {
        if (seed.Depots.Count == 0)
        {
            return;
        }

        var hueDepot = seed.Depots[0];
        var babyFormulaModel = seed.ItemModels.Single(model =>
            string.Equals(model.Name, "Sữa bột trẻ em", StringComparison.OrdinalIgnoreCase));

        EnsureDepotInventory(seed, hueDepot.Id, babyFormulaModel.Id, 0, 0);
    }

    private static void EnsureEssentialBlanketLots(DemoSeedContext seed, ItemModel blanketModel)
    {
        var lotInventoryIds = seed.Lots
            .Select(l => l.SupplyInventoryId)
            .ToHashSet();
        var blanketInventories = seed.Inventories
            .Where(i => i.ItemModelId == blanketModel.Id)
            .OrderBy(i => i.DepotId)
            .ToList();

        foreach (var inventory in blanketInventories)
        {
            if (lotInventoryIds.Contains(inventory.Id))
            {
                continue;
            }

            var received = seed.AnchorUtc.AddDays(-45 - (inventory.DepotId ?? 0));
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = inventory.Quantity ?? 0,
                RemainingQuantity = Math.Max(0, (inventory.Quantity ?? 0) - inventory.MissionReservedQuantity - inventory.TransferReservedQuantity),
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(18),
                SourceType = InventorySourceType.Donation.ToString(),
                SourceId = 4_000 + inventory.Id,
                CreatedAt = received
            });
        }
    }

    private static void EnsureHueDepotExpiringConsumableLots(DemoSeedContext seed)
    {
        if (seed.Depots.Count == 0)
        {
            return;
        }

        var hueDepot = seed.Depots[0];
        var specs = new (string ItemName, int Quantity, int ReceivedOffsetDays, int ExpiredOffsetDays, int SourceId)[]
        {
            ("Mì tôm", 24, -20, 7, 90_001),
            ("Nước tinh khiết", 48, -18, 14, 90_002),
            ("Thuốc hạ sốt Paracetamol 500mg", 60, -14, 28, 90_004)
        };

        foreach (var spec in specs)
        {
            var itemModel = seed.ItemModels.Single(model =>
                string.Equals(model.Name, spec.ItemName, StringComparison.OrdinalIgnoreCase));
            var inventory = seed.Inventories.Single(inventory =>
                inventory.DepotId == hueDepot.Id && inventory.ItemModelId == itemModel.Id);
            var receivedDate = seed.AnchorUtc.AddDays(spec.ReceivedOffsetDays);

            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = spec.Quantity,
                RemainingQuantity = spec.Quantity,
                ReceivedDate = receivedDate,
                ExpiredDate = seed.AnchorUtc.AddDays(spec.ExpiredOffsetDays),
                SourceType = InventorySourceType.Purchase.ToString(),
                SourceId = spec.SourceId,
                CreatedAt = receivedDate
            });

            inventory.Quantity = (inventory.Quantity ?? 0) + spec.Quantity;
            inventory.LastStockedAt = receivedDate;
        }
    }

    private static void EnsureClosureTestDepotsConsumableLots(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        var lotInventoryIds = seed.Lots
            .Select(lot => lot.SupplyInventoryId)
            .ToHashSet();

        foreach (var closureDepot in closureDepots)
        {
            foreach (var inventory in seed.Inventories
                         .Where(i => i.DepotId == closureDepot.Id && i.ItemModelId.HasValue)
                         .OrderBy(i => i.ItemModelId))
            {
                if (lotInventoryIds.Contains(inventory.Id))
                {
                    continue;
                }

                var itemModel = seed.ItemModels.Single(model => model.Id == inventory.ItemModelId!.Value);
                if (!string.Equals(itemModel.ItemType, nameof(ItemType.Consumable), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var receivedDate = seed.AnchorUtc.AddDays(-(45 + itemModel.Id % 60));
                seed.Lots.Add(new SupplyInventoryLot
                {
                    SupplyInventoryId = inventory.Id,
                    Quantity = inventory.Quantity ?? 0,
                    RemainingQuantity = inventory.Quantity ?? 0,
                    ReceivedDate = receivedDate,
                    ExpiredDate = receivedDate.AddMonths(8 + itemModel.Id % 10),
                    SourceType = InventorySourceType.Donation.ToString(),
                    SourceId = 120_000 + itemModel.Id,
                    CreatedAt = receivedDate
                });
            }
        }
    }

    private static void EnsureLifeJacketReusableUnits(DemoSeedContext seed, ItemModel lifeJacketModel)
    {
        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            var targetQuantity = EssentialLifeJacketQuantity(depotIndex);
            var existingCount = seed.ReusableItems.Count(item =>
                item.DepotId == depot.Id && item.ItemModelId == lifeJacketModel.Id);

            for (var unitIndex = existingCount; unitIndex < targetQuantity; unitIndex++)
            {
                var serialNumber = $"LIFEJACKET-D{depot.Id:00}-{unitIndex + 1:000}";
                if (!existingSerials.Add(serialNumber))
                {
                    continue;
                }

                seed.ReusableItems.Add(new ReusableItem
                {
                    DepotId = depot.Id,
                    ItemModelId = lifeJacketModel.Id,
                    SerialNumber = serialNumber,
                    Status = unitIndex % 19 == 0 ? "Maintenance" : "Available",
                    Condition = unitIndex % 23 == 0 ? "Fair" : "Good",
                    Note = unitIndex % 19 == 0 ? "Kiểm tra định kỳ trước mùa mưa bão" : null,
                    CreatedAt = seed.AnchorUtc.AddDays(-90 + (depotIndex * 7 + unitIndex) % 60),
                    UpdatedAt = seed.AnchorUtc.AddDays(-((depotIndex + unitIndex) % 25)),
                    IsDeleted = false
                });
            }
        }
    }

    private static void EnsureClosureTestDepotsReusableUnits(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var closureDepot in closureDepots)
        {
            foreach (var itemModel in seed.ItemModels
                         .Where(model => string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
                         .OrderBy(model => model.Id))
            {
                foreach (var existingItem in seed.ReusableItems.Where(item =>
                             item.DepotId == closureDepot.Id
                             && item.ItemModelId == itemModel.Id))
                {
                    existingItem.Status = ReusableItemStatus.Available.ToString();
                    existingItem.Note = ClosureTestDepotReusableNote(closureDepot);
                    existingItem.UpdatedAt = seed.AnchorUtc.AddDays(-((itemModel.Id + existingItem.Id) % 18));
                }

                var targetQuantity = ClosureTestDepotQuantity(itemModel);
                var existingCount = seed.ReusableItems.Count(item =>
                    item.DepotId == closureDepot.Id && item.ItemModelId == itemModel.Id);

                for (var unitIndex = existingCount; unitIndex < targetQuantity; unitIndex++)
                {
                    var serialNumber = $"PHY-DEPOT-D{closureDepot.Id:00}-M{itemModel.Id:000}-{unitIndex + 1:000}";
                    if (!existingSerials.Add(serialNumber))
                    {
                        continue;
                    }

                    seed.ReusableItems.Add(new ReusableItem
                    {
                        DepotId = closureDepot.Id,
                        ItemModelId = itemModel.Id,
                        SerialNumber = serialNumber,
                        Status = ReusableItemStatus.Available.ToString(),
                        Condition = "Good",
                        Note = ClosureTestDepotReusableNote(closureDepot),
                        CreatedAt = seed.AnchorUtc.AddDays(-(60 + (itemModel.Id + unitIndex) % 45)),
                        UpdatedAt = seed.AnchorUtc.AddDays(-((itemModel.Id + unitIndex) % 18)),
                        IsDeleted = false
                    });
                }
            }
        }
    }

    private static int ClosureTestDepotQuantity(ItemModel itemModel) =>
        string.Equals(itemModel.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)
            ? 4 + itemModel.Id % 3
            : 120 + (itemModel.Id % 5) * 20;

    private static string ClosureTestDepotReusableNote(Depot depot) =>
        $"Kho test đóng kho {depot.Name} - vật tư sẵn sàng chuyển kho.";

    private static void EnsureManagerReturnFixtureReusableUnits(DemoSeedContext seed)
    {
        if (seed.Depots.Count == 0)
        {
            return;
        }

        var hueDepot = seed.Depots[0];
        var reusableModelIdsWithEnoughUnits = seed.ReusableItems
            .Where(item => item.DepotId == hueDepot.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal)
                && item.ItemModelId.HasValue
                && seed.ItemModels.Any(model =>
                    model.Id == item.ItemModelId.Value
                    && string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.ItemModelId!.Value)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Key)
            .ToHashSet();

        if (reusableModelIdsWithEnoughUnits.Count >= 2)
        {
            return;
        }

        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateModels = seed.ItemModels
            .Where(model => string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
            .OrderBy(model => model.Id)
            .ToList();

        foreach (var model in candidateModels)
        {
            if (reusableModelIdsWithEnoughUnits.Count >= 2)
            {
                break;
            }

            if (reusableModelIdsWithEnoughUnits.Contains(model.Id))
            {
                continue;
            }

            var availableCount = seed.ReusableItems.Count(item =>
                item.DepotId == hueDepot.Id
                && item.ItemModelId == model.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal));

            for (var unitIndex = availableCount; unitIndex < 2; unitIndex++)
            {
                var serialNumber = $"RETURN-FIXTURE-D{hueDepot.Id:00}-M{model.Id:000}-{unitIndex + 1:000}";
                if (!existingSerials.Add(serialNumber))
                {
                    continue;
                }

                seed.ReusableItems.Add(new ReusableItem
                {
                    DepotId = hueDepot.Id,
                    ItemModelId = model.Id,
                    SerialNumber = serialNumber,
                    Status = ReusableItemStatus.Available.ToString(),
                    Condition = "Good",
                    Note = "Demo manager01 return fixture reusable unit.",
                    CreatedAt = seed.AnchorUtc.AddDays(-45 - unitIndex),
                    UpdatedAt = seed.AnchorUtc.AddDays(-7 - unitIndex),
                    IsDeleted = false
                });
            }

            reusableModelIdsWithEnoughUnits.Add(model.Id);
        }
    }

    private static void RecomputeSeedDepotUtilization(DemoSeedContext seed)
    {
        var itemModelsById = seed.ItemModels.ToDictionary(model => model.Id);
        var loadByDepot = new Dictionary<int, (decimal Volume, decimal Weight)>();

        foreach (var inventory in seed.Inventories.Where(item =>
                     item.DepotId.HasValue
                     && item.ItemModelId.HasValue
                     && !item.IsDeleted
                     && itemModelsById.ContainsKey(item.ItemModelId.Value)))
        {
            var itemModel = itemModelsById[inventory.ItemModelId!.Value];
            var quantity = inventory.Quantity ?? 0;
            AddSeedDepotLoad(
                loadByDepot,
                inventory.DepotId!.Value,
                quantity * (itemModel.VolumePerUnit ?? 0m),
                quantity * (itemModel.WeightPerUnit ?? 0m));
        }

        foreach (var reusableItem in seed.ReusableItems.Where(item =>
                     item.DepotId.HasValue
                     && item.ItemModelId.HasValue
                     && !item.IsDeleted
                     && !string.Equals(item.Status, ReusableItemStatus.Decommissioned.ToString(), StringComparison.Ordinal)
                     && itemModelsById.ContainsKey(item.ItemModelId.Value)))
        {
            var itemModel = itemModelsById[reusableItem.ItemModelId!.Value];
            AddSeedDepotLoad(
                loadByDepot,
                reusableItem.DepotId!.Value,
                itemModel.VolumePerUnit ?? 0m,
                itemModel.WeightPerUnit ?? 0m);
        }

        foreach (var depot in seed.Depots)
        {
            var load = loadByDepot.GetValueOrDefault(depot.Id);
            var currentVolume = decimal.Round(load.Volume, 3, MidpointRounding.AwayFromZero);
            var currentWeight = decimal.Round(load.Weight, 3, MidpointRounding.AwayFromZero);

            depot.CurrentUtilization = currentVolume;
            depot.CurrentWeightUtilization = currentWeight;
            if (currentVolume > (depot.Capacity ?? 0m))
            {
                depot.Capacity = decimal.Round(currentVolume * 1.2m, 3, MidpointRounding.AwayFromZero);
            }

            if (currentWeight > (depot.WeightCapacity ?? 0m))
            {
                depot.WeightCapacity = decimal.Round(currentWeight * 1.2m, 3, MidpointRounding.AwayFromZero);
            }
        }
    }

    private static void AddSeedDepotLoad(
        IDictionary<int, (decimal Volume, decimal Weight)> loadByDepot,
        int depotId,
        decimal volume,
        decimal weight)
    {
        loadByDepot.TryGetValue(depotId, out var current);
        loadByDepot[depotId] = (current.Volume + volume, current.Weight + weight);
    }

    private static int EssentialLifeJacketQuantity(int depotIndex) =>
        50 + (35 + depotIndex * 13) % 51;

    private static int EssentialBlanketQuantity(int depotIndex) =>
        50 + (42 + depotIndex * 17) % 51;

    private static int? ResolveVatInvoiceId(IReadOnlyList<int> vatInvoiceIds, string sourceType, int? sourceId)
    {
        if (!string.Equals(sourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal) || vatInvoiceIds.Count == 0)
        {
            return null;
        }

        if (sourceId.HasValue && vatInvoiceIds.Contains(sourceId.Value))
        {
            return sourceId.Value;
        }

        return vatInvoiceIds[Math.Abs((sourceId ?? 1) - 1) % vatInvoiceIds.Count];
    }


    private static IReadOnlyList<ItemTemplate> BaseItemModels()
    {
        return
        [
            new("Food", "Mì tôm", "Mì ăn liền đóng gói dùng cứu trợ khẩn cấp", "gói", "Consumable", 0.8m, 0.075m),
            new("Food", "Sữa bột trẻ em", "Sữa bột dinh dưỡng dành cho trẻ em dưới 6 tuổi", "gói", "Consumable", 0.5m, 0.4m),
            new("Food", "Lương khô", "Lương khô năng lượng cao, bảo quản lâu dài", "thanh", "Consumable", 0.15m, 0.06m),
            new("Food", "Gạo sấy khô", "Gạo sấy khô ăn liền, chỉ cần thêm nước nóng", "gói", "Consumable", 0.6m, 0.5m),
            new("Food", "Cháo ăn liền", "Cháo ăn liền đóng gói, dễ tiêu hóa cho mọi lứa tuổi", "gói", "Consumable", 0.4m, 0.065m),
            new("Food", "Bánh mì khô", "Bánh mì khô bảo quản lâu, tiện lợi khi cứu trợ", "gói", "Consumable", 0.8m, 0.15m),
            new("Food", "Muối tinh", "Muối tinh tiêu chuẩn dùng chế biến thực phẩm", "gói", "Consumable", 0.2m, 0.25m),
            new("Food", "Đường cát trắng", "Đường cát trắng tinh luyện dùng pha chế và nấu ăn", "gói", "Consumable", 0.35m, 0.5m),
            new("Food", "Dầu ăn thực vật", "Dầu ăn thực vật đóng chai dùng chế biến thực phẩm", "chai", "Consumable", 1.2m, 1.0m),
            new("Food", "Thịt hộp đóng gói", "Thịt hộp đóng gói bảo quản lâu, giàu dinh dưỡng", "hộp", "Consumable", 0.5m, 0.35m),
            new("Water", "Nước tinh khiết", "Nước uống đóng chai 500ml phục vụ cấp phát", "chai", "Consumable", 0.6m, 0.52m),
            new("Water", "Nước lọc bình 20L", "Bình nước lọc 20 lít phục vụ sinh hoạt tập thể", "bình", "Consumable", 22.0m, 20.5m),
            new("Water", "Viên lọc nước khẩn cấp", "Viên lọc nước cầm tay, xử lý nước bẩn thành nước uống", "viên", "Consumable", 0.005m, 0.004m),
            new("Water", "Chai nước Aquafina", "Nước tinh khiết Aquafina đóng chai 500ml", "chai", "Consumable", 0.6m, 0.53m),
            new("Water", "Nước khoáng thiên nhiên 500ml", "Nước khoáng thiên nhiên đóng chai 500ml", "chai", "Consumable", 0.6m, 0.53m),
            new("Water", "Nước dừa đóng hộp", "Nước dừa tươi đóng hộp bổ sung điện giải", "hộp", "Consumable", 0.4m, 0.35m),
            new("Water", "Bột bù điện giải ORS", "Bột pha bù nước và điện giải cho người mất nước", "gói", "Consumable", 0.05m, 0.025m),
            new("Medical", "Thuốc hạ sốt Paracetamol 500mg", "Thuốc hạ sốt giảm đau cơ bản cho người lớn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dầu gió", "Dầu gió xanh dùng xoa bóp giảm đau, chống cảm", "chai", "Consumable", 0.04m, 0.035m),
            new("Medical", "Sắt & Vitamin tổng hợp", "Viên uống bổ sung sắt và vitamin tổng hợp", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Băng gạc y tế vô khuẩn", "Băng gạc vô khuẩn dùng băng bó vết thương", "cuộn", "Consumable", 0.15m, 0.05m),
            new("Medical", "Bông gòn y tế", "Bông gòn y tế vô khuẩn dùng vệ sinh và sơ cứu", "gói", "Consumable", 0.4m, 0.05m),
            new("Medical", "Thuốc kháng sinh Amoxicillin", "Thuốc kháng sinh phổ rộng điều trị nhiễm khuẩn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dung dịch sát khuẩn Betadine", "Dung dịch sát khuẩn Povidone-Iodine rửa vết thương", "chai", "Consumable", 0.15m, 0.12m),
            new("Medical", "Khẩu trang y tế 3 lớp", "Khẩu trang y tế dùng một lần, đóng gói vô khuẩn", "chiếc", "Consumable", 0.04m, 0.005m),
            new("Medical", "Bộ sơ cứu cơ bản", "Bộ sơ cứu gồm băng, gạc, kéo, kẹp và thuốc cơ bản", "bộ", "Consumable", 3.0m, 1.5m),
            new("Medical", "Thuốc tiêu chảy Loperamide", "Cực kỳ cần vì nước bẩn dễ gây tiêu chảy cấp", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc trị nhiễm khuẩn đường ruột Smecta", "Bảo vệ niêm mạc ruột, dùng phổ biến cho trẻ em", "gói", "Consumable", 0.02m, 0.015m),
            new("Medical", "Thuốc chống nôn Domperidone", "Hữu ích khi ngộ độc thực phẩm, say nước", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc cảm cúm tổng hợp Decolgen", "Giảm triệu chứng cảm lạnh do thời tiết ẩm ướt", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc ho Dextromethorphan", "Ho do nhiễm lạnh, viêm họng", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc long đờm Acetylcysteine", "Giúp thông đường hô hấp", "gói", "Consumable", 0.02m, 0.015m),
            new("Medical", "Thuốc chống dị ứng Loratadine", "Rất cần trong môi trường nhiều muỗi, côn trùng", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Kem bôi ngoài da Hydrocortisone", "Dùng cho viêm da, ngứa, phát ban", "tuýp", "Consumable", 0.08m, 0.05m),
            new("Medical", "Thuốc chống nấm da Clotrimazole", "Quan trọng vì ngâm nước lâu dễ bị nấm", "tuýp", "Consumable", 0.08m, 0.05m),
            new("Medical", "Thuốc giảm đau kháng viêm Ibuprofen", "Dùng khi chấn thương nhẹ, đau cơ", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc nhỏ mắt (viêm kết mạc)", "Điều trị viêm kết mạc do nước bẩn", "chai", "Consumable", 0.05m, 0.04m),
            new("Medical", "Thuốc nhỏ mũi (nghẹt mũi do lạnh)", "Giảm nghẹt mũi do thời tiết lạnh", "chai", "Consumable", 0.05m, 0.04m),
            new("Medical", "Vitamin C liều cao", "Tăng cường sức đề kháng", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Thuốc chống say nước", "Dành cho đội cứu hộ khi làm việc trên nước", "viên", "Consumable", 0.005m, 0.002m),
            new("Hygiene", "Băng vệ sinh", "Băng vệ sinh phụ nữ dùng một lần, đóng gói riêng", "miếng", "Consumable", 0.06m, 0.015m),
            new("Hygiene", "Xà phòng diệt khuẩn", "Xà phòng cục diệt khuẩn dùng vệ sinh cá nhân", "bánh", "Consumable", 0.12m, 0.1m),
            new("Hygiene", "Nước rửa tay khô", "Gel rửa tay khô diệt khuẩn nhanh, không cần nước", "chai", "Consumable", 0.3m, 0.28m),
            new("Hygiene", "Khăn ướt kháng khuẩn", "Khăn ướt kháng khuẩn tiện dụng, đóng gói 10 tờ", "gói", "Consumable", 0.25m, 0.1m),
            new("Hygiene", "Kem đánh răng", "Kem đánh răng kích thước nhỏ gọn phù hợp cứu trợ", "tuýp", "Consumable", 0.15m, 0.12m),
            new("Hygiene", "Bàn chải đánh răng", "Bàn chải đánh răng dùng một lần, đóng gói riêng", "chiếc", "Consumable", 0.06m, 0.02m),
            new("Hygiene", "Dầu gội đầu", "Dầu gội đầu gói nhỏ tiện lợi cho cứu trợ", "chai", "Consumable", 0.25m, 0.22m),
            new("Hygiene", "Khăn bông tắm", "Khăn bông tắm cỡ trung dùng vệ sinh cá nhân", "chiếc", "Consumable", 2.5m, 0.35m),
            new("Hygiene", "Giấy vệ sinh", "Giấy vệ sinh cuộn nhỏ tiêu chuẩn", "cuộn", "Consumable", 1.2m, 0.1m),
            new("Hygiene", "Tã dùng một lần", "Tã giấy dùng một lần cho trẻ em hoặc người già", "miếng", "Consumable", 0.5m, 0.06m),
            new("Clothing", "Áo mưa người lớn", "Áo mưa nhựa dùng một lần cho người lớn", "chiếc", "Consumable", 1.5m, 0.25m),
            new("Clothing", "Ủng cao su chống lũ", "Ủng cao su chống nước dùng đi lại trong vùng ngập", "đôi", "Consumable", 6.0m, 1.8m),
            new("Clothing", "Bộ quần áo trẻ em", "Bộ quần áo sạch kích thước trẻ em 3–12 tuổi", "bộ", "Consumable", 2.0m, 0.3m),
            new("Clothing", "Áo ấm người lớn", "Áo khoác giữ ấm dùng trong thời tiết lạnh", "chiếc", "Consumable", 4.0m, 0.7m),
            new("Clothing", "Bộ quần áo người lớn", "Bộ quần áo sạch kích thước người lớn", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Bộ quần áo người cao tuổi", "Bộ quần áo thoải mái phù hợp người cao tuổi", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Găng tay giữ ấm", "Găng tay len giữ ấm trong thời tiết lạnh", "đôi", "Consumable", 0.3m, 0.08m),
            new("Clothing", "Tất len giữ ấm", "Tất len dày giữ ấm chân trong mùa lạnh", "đôi", "Consumable", 0.2m, 0.06m),
            new("Clothing", "Mũ len", "Mũ len giữ ấm đầu trong thời tiết lạnh", "chiếc", "Consumable", 0.4m, 0.08m),
            new("Clothing", "Áo mưa trẻ em", "Áo mưa nhựa dùng một lần cho trẻ em", "chiếc", "Consumable", 1.0m, 0.18m),
            new("Shelter", "Lều bạt cứu trợ 4 người", "Lều bạt dã chiến sức chứa 4 người, chống nước", "chiếc", "Consumable", 30.0m, 8.0m),
            new("Shelter", "Tấm bạt che mưa đa năng", "Tấm bạt PE chống nước đa năng dùng che mưa nắng", "tấm", "Consumable", 5.0m, 1.5m),
            new("Shelter", "Túi ngủ giữ nhiệt", "Túi ngủ cách nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 10.0m, 1.8m),
            new("Shelter", "Đệm hơi dã chiến", "Đệm hơi gấp gọn dùng ngủ dã chiến", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Shelter", "Màn chống côn trùng", "Màn lưới chống muỗi và côn trùng khi ngủ", "chiếc", "Consumable", 2.0m, 0.4m),
            new("Shelter", "Bộ cọc và dây lều", "Bộ cọc kim loại và dây buộc để dựng lều", "bộ", "Reusable", 3.0m, 2.0m),
            new("Shelter", "Tấm bạt chống thấm", "Tấm bạt PE dày chống thấm nước dùng lót sàn lều", "tấm", "Consumable", 4.0m, 1.2m),
            new("Shelter", "Dây buộc đa năng", "Dây thừng đa năng dùng buộc, cố định vật dụng", "cuộn", "Reusable", 2.0m, 1.5m),
            new("Shelter", "Đèn LED dã chiến", "Đèn LED sạc dùng chiếu sáng dã chiến", "chiếc", "Reusable", 1.0m, 0.35m),
            new("Shelter", "Nến khẩn cấp", "Nến cháy lâu dùng chiếu sáng khi mất điện", "cây", "Consumable", 0.15m, 0.12m),
            new("RepairTools", "Búa đóng đinh", "Búa sắt đóng đinh dùng sửa chữa nhà cửa", "chiếc", "Reusable", 1.5m, 0.5m),
            new("RepairTools", "Đinh các loại", "Bộ đinh sắt các kích cỡ dùng sửa chữa", "gói", "Consumable", 0.3m, 0.5m),
            new("RepairTools", "Cưa tay đa năng", "Cưa tay gấp gọn dùng cắt gỗ và vật liệu", "chiếc", "Reusable", 3.0m, 0.6m),
            new("RepairTools", "Tua vít 2 đầu", "Tua vít 2 đầu dẹt và bake dùng sửa chữa", "chiếc", "Reusable", 0.3m, 0.15m),
            new("RepairTools", "Kìm cắt dây", "Kìm cắt dây thép và dây điện đa năng", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RepairTools", "Băng keo chống thấm", "Băng keo dán chống thấm nước cho mái và tường", "cuộn", "Consumable", 0.2m, 0.15m),
            new("RepairTools", "Dao đa năng dã chiến", "Dao gấp đa năng tích hợp nhiều công cụ", "chiếc", "Reusable", 0.2m, 0.2m),
            new("RepairTools", "Xẻng tay", "Xẻng tay gấp gọn dùng đào đắp trong cứu trợ", "chiếc", "Reusable", 4.0m, 1.2m),
            new("RepairTools", "Bao cát chống lũ", "Bao cát dùng đắp đê ngăn nước lũ tràn", "chiếc", "Reusable", 2.5m, 0.4m),
            new("RepairTools", "Bộ dụng cụ sửa chữa điện cơ bản", "Bộ dụng cụ sửa chữa điện gồm kìm, tua vít, băng keo", "bộ", "Reusable", 4.0m, 2.5m),
            new("RescueEquipment", "Áo phao cứu sinh", "Áo phao tiêu chuẩn phục vụ cứu hộ đường thủy", "chiếc", "Reusable", 8.0m, 1.2m),
            new("RescueEquipment", "Bình lọc nước dã chiến", "Bình lọc nước di động lọc nước bẩn thành nước sạch", "chiếc", "Reusable", 5.0m, 2.0m),
            new("RescueEquipment", "Can đựng nước 10L", "Can nhựa 10 lít chứa và vận chuyển nước sạch", "chiếc", "Reusable", 12.0m, 0.8m),
            new("RescueEquipment", "Túi đựng nước linh hoạt", "Túi nhựa dẻo đựng nước gấp gọn khi không sử dụng", "chiếc", "Reusable", 1.5m, 0.3m),
            new("RescueEquipment", "Nhiệt kế điện tử", "Nhiệt kế điện tử đo thân nhiệt nhanh chóng", "chiếc", "Reusable", 0.1m, 0.05m),
            new("RescueEquipment", "Xuồng cao su cứu hộ", "Xuồng cao su chuyên dụng cho nhiệm vụ cứu hộ lũ", "chiếc", "Reusable", 250.0m, 45.0m),
            new("RescueEquipment", "Dây thừng cứu sinh 30m", "Dây thừng dài 30m chịu lực cao dùng cứu hộ", "cuộn", "Reusable", 6.0m, 3.5m),
            new("RescueEquipment", "Phao tròn cứu sinh", "Phao tròn cứu sinh tiêu chuẩn ném cho nạn nhân", "chiếc", "Reusable", 20.0m, 2.5m),
            new("RescueEquipment", "Máy bơm nước di động", "Máy bơm nước chạy xăng di động hút nước ngập", "chiếc", "Reusable", 60.0m, 25.0m),
            new("RescueEquipment", "Bộ đàm liên lạc dã chiến", "Bộ đàm cầm tay liên lạc tần số UHF/VHF", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RescueEquipment", "Đèn tín hiệu khẩn cấp", "Đèn tín hiệu nhấp nháy cảnh báo khu vực nguy hiểm", "chiếc", "Reusable", 0.8m, 0.4m),
            new("RescueEquipment", "Máy phát điện di động", "Máy phát điện xăng di động công suất nhỏ", "chiếc", "Reusable", 120.0m, 50.0m),
            new("RescueEquipment", "Cáng khiêng thương", "Cáng gấp gọn dùng vận chuyển người bị thương", "chiếc", "Reusable", 30.0m, 7.0m),
            new("RescueEquipment", "Mũ bảo hiểm cứu hộ", "Mũ bảo hiểm chuyên dụng cho cứu hộ viên", "chiếc", "Reusable", 6.0m, 0.6m),
            new("Heating", "Chăn ấm giữ nhiệt", "Chăn dày giữ nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 6.0m, 1.5m),
            new("Heating", "Than tổ ong", "Than tổ ong dùng đốt sưởi ấm hoặc nấu ăn", "viên", "Consumable", 1.2m, 1.0m),
            new("Heating", "Máy sưởi điện mini", "Máy sưởi điện nhỏ gọn công suất thấp", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Heating", "Túi sưởi ấm tay dùng một lần", "Túi sưởi ấm tay phản ứng hóa học dùng một lần", "gói", "Consumable", 0.05m, 0.04m),
            new("Heating", "Bộ quần áo nhiệt", "Bộ đồ lót giữ nhiệt mặc trong thời tiết rét", "bộ", "Consumable", 2.5m, 0.4m),
            new("Heating", "Ấm đun nước du lịch", "Ấm đun nước điện nhỏ gọn tiện dùng dã chiến", "chiếc", "Consumable", 3.0m, 0.8m),
            new("Heating", "Bếp gas du lịch mini", "Bếp gas mini gấp gọn dùng nấu ăn dã chiến", "chiếc", "Consumable", 4.0m, 1.5m),
            new("Heating", "Bình gas mini dã chiến", "Bình gas lon nhỏ dùng cho bếp gas du lịch", "bình", "Consumable", 0.8m, 0.35m),
            new("Heating", "Chăn điện sưởi", "Chăn điện sưởi ấm dùng khi ngủ mùa lạnh", "chiếc", "Consumable", 5.0m, 1.8m),
            new("Heating", "Tấm sưởi ấm bức xạ", "Tấm sưởi hồng ngoại bức xạ di động", "chiếc", "Consumable", 15.0m, 5.0m),
            new("Vehicle", "Xe tải cứu trợ 2.5 tấn", "Xe tải 2.5 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 18000.0m, 3500.0m),
            new("Vehicle", "Xe cứu thương", "Xe chuyên dụng vận chuyển cấp cứu và bệnh nhân", "chiếc", "Reusable", 16000.0m, 3800.0m),
            new("Vehicle", "Xe bán tải 4x4", "Xe bán tải 2 cầu vượt địa hình xấu", "chiếc", "Reusable", 12000.0m, 2200.0m),
            new("Vehicle", "Xe máy địa hình", "Xe máy địa hình đi vào vùng khó tiếp cận", "chiếc", "Reusable", 2500.0m, 150.0m),
            new("Vehicle", "Ca nô cứu hộ", "Ca nô máy chuyên dụng cứu hộ đường thủy", "chiếc", "Reusable", 8000.0m, 800.0m),
            new("Vehicle", "Xe chở hàng nhẹ 1 tấn", "Xe tải nhẹ 1 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 14000.0m, 2500.0m),
            new("Vehicle", "Xe tải đông lạnh 3.5 tấn", "Xe tải đông lạnh bảo quản thực phẩm tươi sống", "chiếc", "Reusable", 20000.0m, 5000.0m),
            new("Vehicle", "Xe khách 16 chỗ", "Xe khách 16 chỗ chở người sơ tán", "chiếc", "Reusable", 15000.0m, 3200.0m),
            new("Vehicle", "Xe cẩu di động", "Xe cẩu di động dọn dẹp đổ nát và vật cản", "chiếc", "Reusable", 20000.0m, 12000.0m),
            new("Vehicle", "Xe chuyên dụng phòng cháy", "Xe chữa cháy chuyên dụng phòng cháy chữa cháy", "chiếc", "Reusable", 18000.0m, 8000.0m),
            new("Others", "Pin dự phòng 10000mAh", "Pin sạc dự phòng 10000mAh sạc điện thoại", "chiếc", "Consumable", 0.25m, 0.22m),
            new("Others", "Cáp sạc đa năng", "Cáp sạc đa đầu Lightning/USB-C/Micro USB", "chiếc", "Consumable", 0.08m, 0.04m),
            new("Others", "Bản đồ địa hình khẩn cấp", "Bản đồ in địa hình khu vực thường xảy ra thiên tai", "tờ", "Consumable", 0.1m, 0.05m),
            new("Others", "Còi báo động khẩn cấp", "Còi thổi báo động và kêu gọi cứu hộ khẩn cấp", "chiếc", "Consumable", 0.02m, 0.015m),
            new("Others", "Kính bảo hộ lao động", "Kính bảo hộ chống bụi và mảnh vỡ khi làm việc", "chiếc", "Reusable", 0.3m, 0.08m),
            new("Others", "Ba lô khẩn cấp", "Ba lô chứa đồ dùng thiết yếu cho tình huống khẩn cấp", "chiếc", "Consumable", 25.0m, 0.8m),
            new("Others", "Sổ tay và bút ghi chép", "Bộ sổ tay và bút bi dùng ghi chép thông tin hiện trường", "bộ", "Consumable", 0.3m, 0.18m),
            new("Others", "Bộ đèn pin đội đầu", "Đèn pin LED đội đầu rọi sáng rảnh tay", "bộ", "Reusable", 0.5m, 0.15m),
            new("Others", "Áo phản quang an toàn", "Áo ghi lê phản quang tăng nhận diện trong đêm", "chiếc", "Reusable", 1.5m, 0.2m),
            new("Others", "Pháo sáng khẩn cấp", "Pháo sáng phát tín hiệu cầu cứu khẩn cấp", "chiếc", "Consumable", 0.25m, 0.15m)
        ];
    }

    private static IReadOnlyList<int> ReliefItemImageIdsInSeedOrder()
    {
        return
        [
            1, 7, 8, 11, 12, 13, 14, 15, 16, 17,
            2, 18, 19, 20, 22, 25, 26,
            3, 9, 10, 27, 28, 29, 30, 32, 33, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124,
            5, 34, 35, 36, 37, 38, 39, 40, 41, 42,
            43, 44, 45, 46, 47, 48, 49, 50, 51, 52,
            53, 54, 55, 56, 57, 58, 59, 60, 61, 62,
            63, 64, 65, 66, 67, 68, 69, 70, 71, 72,
            4, 21, 23, 24, 31, 73, 74, 75, 76, 77, 78, 79, 80, 81,
            6, 82, 83, 84, 85, 86, 87, 88, 89, 90,
            101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
            91, 92, 93, 94, 95, 96, 97, 98, 99, 100
        ];
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

    private static IReadOnlyList<string> TargetGroupNamesFor(ItemTemplate template)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static bool HasAny(string value, params string[] patterns) =>
            patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        void Add(params string[] names)
        {
            foreach (var name in names)
            {
                groups.Add(name);
            }
        }

        switch (template.CategoryCode)
        {
            case "FOOD":
            case "WATER":
            case "MEDICINE":
            case "HYGIENE":
            case "CLOTHING":
            case "SHELTER":
            case "HEATING":
            case "OTHERS":
                Add("Adult");
                break;
            case "REPAIR_TOOLS":
            case "RESCUE_EQUIPMENT":
            case "VEHICLE":
                Add("Rescuer");
                break;
        }

        if (HasAny(template.Name, "trẻ em"))
        {
            Add("Children");
        }

        if (HasAny(template.Name, "người cao tuổi"))
        {
            Add("Elderly");
        }

        if (HasAny(template.Name, "Băng vệ sinh", "Sắt & Vitamin"))
        {
            Add("Pregnant");
        }

        if (HasAny(template.Name, "Cháo ăn liền", "Chăn ấm giữ nhiệt"))
        {
            Add("Children", "Elderly", "Pregnant");
        }

        if (HasAny(template.Name, "Nước tinh khiết", "Bột bù điện giải ORS"))
        {
            Add("Children", "Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Gạo sấy khô")
        {
            Add("Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Tã dùng một lần")
        {
            Add("Children", "Elderly");
        }

        if (template.CategoryCode == "FOOD" && HasAny(template.Name, "Mì tôm", "Lương khô", "Bánh mì khô", "Thịt hộp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "WATER" && HasAny(template.Name, "Viên lọc nước khẩn cấp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "MEDICINE" && HasAny(template.Name, "Băng gạc", "Bông gòn", "Betadine", "Khẩu trang", "Bộ sơ cứu"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HYGIENE" && HasAny(template.Name, "Nước rửa tay khô", "Khăn ướt kháng khuẩn"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "CLOTHING" && HasAny(template.Name, "Áo mưa người lớn", "Ủng cao su chống lũ"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "SHELTER" && (template.ItemType == "Reusable" || HasAny(template.Name, "Lều bạt", "Tấm bạt chống thấm", "Nến khẩn cấp")))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HEATING" && HasAny(template.Name, "Túi sưởi", "Bếp gas", "Bình gas"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "OTHERS" && HasAny(template.Name, "Pin dự phòng", "Cáp sạc", "Bản đồ", "Còi báo động", "Ba lô", "Bộ đèn pin", "Áo phản quang", "Kính bảo hộ", "Pháo sáng"))
        {
            Add("Rescuer");
        }

        if (groups.Count == 0)
        {
            Add("Adult");
        }

        return groups.ToList();
    }

    private static string SupplierName(int index)
    {
        var suppliers = new[]
        {
            "Công ty TNHH Thiết bị cứu hộ An Tâm",
            "Công ty CP Nước uống Sông Hương",
            "Nhà thuốc Trung tâm Huế",
            "Công ty TNHH Lương thực miền Trung",
            "Công ty CP Vật tư y tế Đà Nẵng"
        };
        return suppliers[index % suppliers.Length];
    }
}
