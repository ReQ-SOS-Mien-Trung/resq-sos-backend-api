using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;
using RESQ.Infrastructure.Mappers.Resources;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class PurchasedInventoryRepository(IUnitOfWork unitOfWork) : IPurchasedInventoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<bool> ExistsBySerialAndNumberAsync(string invoiceSerial, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<VatInvoice>();
        var existing = await repo.GetByPropertyAsync(
            v => v.InvoiceSerial == invoiceSerial && v.InvoiceNumber == invoiceNumber,
            tracked: false);
        return existing != null;
    }

    public async Task<VatInvoiceModel> CreateVatInvoiceAsync(VatInvoiceModel model, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<VatInvoice>();
        var entity = VatInvoiceMapper.ToEntity(model);
        await repo.AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return VatInvoiceMapper.ToDomain(entity);
    }

    public async Task<List<ReliefItemModel>> GetOrCreateReliefItemsBulkAsync(List<ReliefItemModel> models, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ReliefItem>();
        var results = new List<ReliefItemModel>();

        // Nhóm theo các thuộc tính duy nhất để tránh trùng lặp
        var uniqueItems = models
            .GroupBy(m => new { m.Name, m.CategoryId, m.Unit, m.ItemType, m.TargetGroup })
            .Select(g => g.First())
            .ToList();

        var existingItems = new List<ReliefItem>();
        var newItems = new List<ReliefItem>();

        foreach (var model in uniqueItems)
        {
            var existing = await repo.GetByPropertyAsync(
                r => r.Name == model.Name &&
                     r.CategoryId == model.CategoryId &&
                     r.Unit == model.Unit &&
                     r.ItemType == model.ItemType &&
                     r.TargetGroup == model.TargetGroup,
                tracked: true);

            if (existing != null)
            {
                existingItems.Add(existing);
            }
            else
            {
                var newItem = ReliefItemMapper.ToEntity(model);
                newItems.Add(newItem);
            }
        }

        // Bulk insert các item mới
        if (newItems.Count > 0)
        {
            await repo.AddRangeAsync(newItems);
            await _unitOfWork.SaveAsync();
        }

        // Kết hợp existing và new items
        existingItems.AddRange(newItems);

        foreach (var item in existingItems)
        {
            results.Add(ReliefItemMapper.ToDomain(item));
        }

        return results;
    }

public async Task AddPurchasedInventoryItemsBulkAsync(List<(PurchasedInventoryItemModel model, decimal? unitPrice)> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return;

        // Business Rule: Check depot capacity before any insert
        var depotId = items[0].model.ReceivedAt;
        var totalImportQuantity = items.Sum(x => x.model.Quantity);

        var depotRepo = _unitOfWork.GetRepository<Depot>();
        var depotEntity = await depotRepo.GetByPropertyAsync(d => d.Id == depotId, tracked: true);
        if (depotEntity != null)
        {
            var depotModel = DepotMapper.ToDomain(depotEntity);
            // Throws DepotCapacityExceededException if over limit
            depotModel.UpdateUtilization(totalImportQuantity);
            DepotMapper.UpdateEntity(depotEntity, depotModel);
            await depotRepo.UpdateAsync(depotEntity);
        }

        var orgReliefRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        var inventoryRepo = _unitOfWork.GetRepository<DepotSupplyInventory>();
        var logRepo = _unitOfWork.GetRepository<InventoryLog>();
        var vatInvoiceItemRepo = _unitOfWork.GetRepository<VatInvoiceItem>();

        var orgReliefEntities = new List<OrganizationReliefItem>();
        var inventoryEntities = new List<DepotSupplyInventory>();
        var logEntities = new List<InventoryLog>();
        var vatInvoiceItemEntities = new List<VatInvoiceItem>();

        foreach (var (model, unitPrice) in items)
        {
            // 1. Tạo OrganizationReliefItem với OrganizationId = null (nhập mua, không có tổ chức)
            var orgReliefEntity = new OrganizationReliefItem
            {
                OrganizationId = null,
                ReliefItemId = model.ReliefItemId,
                Quantity = model.Quantity,
                ReceivedDate = model.ReceivedDate,
                ExpiredDate = model.ExpiredDate,
                Notes = model.Notes,
                ReceivedBy = model.ReceivedBy,
                ReceivedAt = model.ReceivedAt,
                CreatedAt = model.CreatedAt ?? DateTime.UtcNow
            };
            orgReliefEntities.Add(orgReliefEntity);

            // 2. Tìm hoặc tạo bản ghi tồn kho
            var existingInventory = await inventoryRepo.GetByPropertyAsync(
                i => i.DepotId == model.ReceivedAt && i.ReliefItemId == model.ReliefItemId,
                tracked: true);

            DepotSupplyInventory inventory;
            if (existingInventory != null)
            {
                existingInventory.Quantity = (existingInventory.Quantity ?? 0) + model.Quantity;
                existingInventory.LastStockedAt = DateTime.UtcNow;
                inventory = existingInventory;
            }
            else
            {
                inventory = new DepotSupplyInventory
                {
                    DepotId = model.ReceivedAt,
                    ReliefItemId = model.ReliefItemId,
                    Quantity = model.Quantity,
                    ReservedQuantity = 0,
                    LastStockedAt = DateTime.UtcNow
                };
                inventoryEntities.Add(inventory);
            }

            // 3. Chuẩn bị log entry
            var logEntity = new InventoryLog
            {
                VatInvoiceId = model.VatInvoiceId,
                ActionType = InventoryActionType.Import.ToString(),
                SourceType = InventorySourceType.Purchase.ToString(),
                QuantityChange = model.Quantity,
                SourceId = null,
                PerformedBy = model.ReceivedBy,
                Note = model.Notes,
                CreatedAt = DateTime.UtcNow
            };
            logEntities.Add(logEntity);

            // 4. Tạo VatInvoiceItem — lưu giá từng món trong hóa đơn VAT
            vatInvoiceItemEntities.Add(new VatInvoiceItem
            {
                VatInvoiceId = model.VatInvoiceId,
                ReliefItemId = model.ReliefItemId,
                Quantity = model.Quantity,
                UnitPrice = unitPrice,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Bulk insert OrganizationReliefItem
        await orgReliefRepo.AddRangeAsync(orgReliefEntities);
        await _unitOfWork.SaveAsync();

        // Bulk insert VatInvoiceItem (dòng giá hóa đơn)
        await vatInvoiceItemRepo.AddRangeAsync(vatInvoiceItemEntities);
        await _unitOfWork.SaveAsync();

        // Bulk insert các bản ghi tồn kho mới
        if (inventoryEntities.Count > 0)
        {
            await inventoryRepo.AddRangeAsync(inventoryEntities);
            await _unitOfWork.SaveAsync();
        }

        // Gắn InventoryId cho log entries rồi bulk insert
        for (int i = 0; i < items.Count; i++)
        {
            var model = items[i].model;
            var inventory = inventoryEntities.FirstOrDefault(inv =>
                inv.DepotId == model.ReceivedAt && inv.ReliefItemId == model.ReliefItemId) ??
                await inventoryRepo.GetByPropertyAsync(
                    inv => inv.DepotId == model.ReceivedAt && inv.ReliefItemId == model.ReliefItemId);

            if (inventory != null)
            {
                logEntities[i].DepotSupplyInventoryId = inventory.Id;
            }
        }

        await logRepo.AddRangeAsync(logEntities);
        await _unitOfWork.SaveAsync();
    }
}
