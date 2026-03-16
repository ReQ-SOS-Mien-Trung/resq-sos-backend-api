using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds test data directly into the live database at app startup.
/// Use this when the database already exists and you cannot recreate it from scratch.
/// Safe to call multiple times — uses a sentinel check to skip if data already exists.
/// </summary>
public static class RuntimeDataSeeder
{
    private static readonly Guid ManagerUserId = new("44444444-4444-4444-4444-444444444444");

    // Sentinel: the Note text that uniquely identifies our seeded logs
    private const string SentinelNote = "Nhập mì tôm theo hóa đơn VAT Q1/2025";

    public static async Task SeedInventoryMovementTestDataAsync(ResQDbContext db)
    {
        // Skip if test data already present (works for both HasData-seeded and runtime-seeded DBs)
        if (await db.InventoryLogs.AnyAsync(l => l.Note == SentinelNote))
            return;

        // ─── VatInvoice 1 : Jan 2025 — mì tôm + nước ───────────────────────
        var inv1 = new VatInvoice
        {
            InvoiceSerial   = "AA",
            InvoiceNumber   = "0001234",
            SupplierName    = "Công ty TNHH Hùng Phúc",
            SupplierTaxCode = "0301234567",
            InvoiceDate     = new DateOnly(2025, 1, 10),
            TotalAmount     = 145_000_000m,
        };
        db.VatInvoices.Add(inv1);
        db.VatInvoiceItems.AddRange(
            new VatInvoiceItem { VatInvoice = inv1, ReliefItemId = 1, Quantity = 20000, UnitPrice =   3_500m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new VatInvoiceItem { VatInvoice = inv1, ReliefItemId = 2, Quantity = 15000, UnitPrice =   5_000m, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ─── VatInvoice 2 : Jan 2026 — thuốc hạ sốt ────────────────────────
        var inv2 = new VatInvoice
        {
            InvoiceSerial   = "AA",
            InvoiceNumber   = "0001235",
            SupplierName    = "Chuỗi Siêu thị Bigmart Huế",
            SupplierTaxCode = "0305678901",
            InvoiceDate     = new DateOnly(2026, 1, 8),
            TotalAmount     = 60_000_000m,
        };
        db.VatInvoices.Add(inv2);
        db.VatInvoiceItems.Add(
            new VatInvoiceItem { VatInvoice = inv2, ReliefItemId = 3, Quantity = 30000, UnitPrice = 2_000m, CreatedAt = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ─── VatInvoice 3 : Feb 2026 — áo phao ─────────────────────────────
        var inv3 = new VatInvoice
        {
            InvoiceSerial   = "BB",
            InvoiceNumber   = "0002001",
            SupplierName    = "Công ty Dược phẩm Minh Châu",
            SupplierTaxCode = "0302345678",
            InvoiceDate     = new DateOnly(2026, 2, 12),
            TotalAmount     = 75_000_000m,
        };
        db.VatInvoices.Add(inv3);
        db.VatInvoiceItems.Add(
            new VatInvoiceItem { VatInvoice = inv3, ReliefItemId = 4, Quantity = 500, UnitPrice = 150_000m, CreatedAt = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Save invoices first so EF assigns their IDs
        await db.SaveChangesAsync();

        // ─── InventoryLogs for Depot 1 ──────────────────────────────────────
        // DepotSupplyInventory IDs: 1=mì tôm, 2=nước, 3=thuốc, 4=áo phao, 6=chăn
        db.InventoryLogs.AddRange(

            // ── Jan 2025 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 1,  VatInvoiceId = inv1.Id,
                ActionType = "Import",      QuantityChange = 20000,
                SourceType = "Purchase",    SourceId = 1,
                PerformedBy = ManagerUserId,
                Note = SentinelNote,                                            // ← sentinel
                CreatedAt = new DateTime(2025, 1, 10, 7, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 2, VatInvoiceId = inv1.Id,
                ActionType = "Import",      QuantityChange = 15000,
                SourceType = "Purchase",    SourceId = 1,
                PerformedBy = ManagerUserId,
                Note = "Nhập nước tinh khiết theo hóa đơn VAT Q1/2025",
                CreatedAt = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 1,
                ActionType = "Export",      QuantityChange = 5000,
                SourceType = "Mission",     MissionId = 1,
                PerformedBy = ManagerUserId,
                Note = "Xuất mì tôm phục vụ nhiệm vụ cứu hộ lũ lụt",
                CreatedAt = new DateTime(2025, 1, 15, 6, 30, 0, DateTimeKind.Utc)
            },

            // ── Jun 2025 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 3,
                ActionType = "Import",      QuantityChange = 30000,
                SourceType = "Donation",    SourceId = 1,
                PerformedBy = ManagerUserId,
                Note = "Nhận thuốc từ Hội Chữ Thập Đỏ TT-Huế đợt 2",
                CreatedAt = new DateTime(2025, 6, 5, 8, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 6,
                ActionType = "Adjust",      QuantityChange = -500,
                SourceType = "Adjustment",
                PerformedBy = ManagerUserId,
                Note = "Điều chỉnh giảm chăn ấm do kiểm kê phát hiện hỏng",
                CreatedAt = new DateTime(2025, 6, 20, 10, 0, 0, DateTimeKind.Utc)
            },

            // ── Oct 2025 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 4,
                ActionType = "Import",      QuantityChange = 1000,
                SourceType = "Donation",    SourceId = 2,
                PerformedBy = ManagerUserId,
                Note = "Nhận áo phao từ UBMTTQVN Đà Nẵng hỗ trợ",
                CreatedAt = new DateTime(2025, 10, 5, 7, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 2,
                ActionType = "TransferOut", QuantityChange = 5000,
                SourceType = "Transfer",    SourceId = 2,
                PerformedBy = ManagerUserId,
                Note = "Chuyển nước uống sang kho Đà Nẵng hỗ trợ bão số 4",
                CreatedAt = new DateTime(2025, 10, 10, 6, 0, 0, DateTimeKind.Utc)
            },

            // ── Jan 2026 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 3, VatInvoiceId = inv2.Id,
                ActionType = "Import",      QuantityChange = 30000,
                SourceType = "Purchase",    SourceId = 2,
                PerformedBy = ManagerUserId,
                Note = "Nhập thuốc Paracetamol theo hóa đơn VAT đầu năm 2026",
                CreatedAt = new DateTime(2026, 1, 8, 8, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 6,
                ActionType = "Export",      QuantityChange = 200,
                SourceType = "Mission",     MissionId = 2,
                PerformedBy = ManagerUserId,
                Note = "Xuất chăn ấm cho đội cứu hộ phân phối vùng lũ",
                CreatedAt = new DateTime(2026, 1, 20, 9, 30, 0, DateTimeKind.Utc)
            },

            // ── Feb 2026 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 4, VatInvoiceId = inv3.Id,
                ActionType = "Import",      QuantityChange = 500,
                SourceType = "Purchase",    SourceId = 3,
                PerformedBy = ManagerUserId,
                Note = "Nhập áo phao cứu sinh mới theo hóa đơn VAT T2/2026",
                CreatedAt = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 4,
                ActionType = "Return",      QuantityChange = 100,
                SourceType = "Mission",     MissionId = 1,
                PerformedBy = ManagerUserId,
                Note = "Hoàn trả áo phao sau khi kết thúc nhiệm vụ cứu hộ",
                CreatedAt = new DateTime(2026, 2, 25, 14, 0, 0, DateTimeKind.Utc)
            },

            // ── Mar 2026 ──────────────────────────────────────────────────────
            new InventoryLog
            {
                DepotSupplyInventoryId = 1,
                ActionType = "Import",      QuantityChange = 10000,
                SourceType = "Donation",    SourceId = 3,
                PerformedBy = ManagerUserId,
                Note = "Tiếp nhận mì tôm từ Quỹ Tấm Lòng Vàng Đà Nẵng",
                CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 3,
                ActionType = "Export",      QuantityChange = 5000,
                SourceType = "Mission",     MissionId = 1,
                PerformedBy = ManagerUserId,
                Note = "Xuất thuốc hạ sốt cấp phát cho vùng thiên tai",
                CreatedAt = new DateTime(2026, 3, 10, 7, 30, 0, DateTimeKind.Utc)
            },
            new InventoryLog
            {
                DepotSupplyInventoryId = 2,
                ActionType = "Adjust",      QuantityChange = -2000,
                SourceType = "Adjustment",
                PerformedBy = ManagerUserId,
                Note = "Điều chỉnh tồn kho nước sau kiểm kê định kỳ quý I/2026",
                CreatedAt = new DateTime(2026, 3, 15, 16, 0, 0, DateTimeKind.Utc)
            }
        );

        await db.SaveChangesAsync();
    }
}
