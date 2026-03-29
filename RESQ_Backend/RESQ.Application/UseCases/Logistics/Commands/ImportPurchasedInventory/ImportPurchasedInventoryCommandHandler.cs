using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandHandler(
    IItemCategoryRepository categoryRepository,
    IPurchasedInventoryRepository purchasedInventoryRepository,
    IDepotInventoryRepository depotInventoryRepository,
    ICampaignDisbursementRepository campaignDisbursementRepository,
    IDepotFundRepository depotFundRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IUnitOfWork unitOfWork,
    ILogger<ImportPurchasedInventoryCommandHandler> logger)
    : IRequestHandler<ImportPurchasedInventoryCommand, ImportPurchasedInventoryResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IPurchasedInventoryRepository _purchasedInventoryRepository = purchasedInventoryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ICampaignDisbursementRepository _disbursementRepo = campaignDisbursementRepository;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ImportPurchasedInventoryCommandHandler> _logger = logger;

    public async Task<ImportPurchasedInventoryResponse> Handle(ImportPurchasedInventoryCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportPurchasedInventoryResponse();

        // 1. Lấy kho đang hoạt động mà người dùng quản lý
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken);
        if (depotId == null)
        {
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động. Không thể nhập hàng.");
        }

        // 2. Tải tất cả danh mục để mapping hiệu quả
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var categoriesByCode = categories
            .ToDictionary(c => c.Code.ToString(), c => c, StringComparer.OrdinalIgnoreCase);

        // 2b. Batch-fetch existing item models for all Path A rows across all groups
        var allItemModelIds = request.Invoices
            .SelectMany(g => g.Items)
            .Where(x => x.ItemModelId.HasValue)
            .Select(x => x.ItemModelId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, ItemModelRecord> existingItemModels;
        if (allItemModelIds.Count > 0)
        {
            existingItemModels = await _itemModelMetadataRepository.GetByIdsAsync(allItemModelIds, cancellationToken);

            var missingIds = allItemModelIds.Where(id => !existingItemModels.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                _logger.LogWarning("Purchase import: {MissingCount} ItemModelId(s) not found in DB: {MissingIds}",
                    missingIds.Count, missingIds);
            }
        }
        else
        {
            existingItemModels = new Dictionary<int, ItemModelRecord>();
        }

        // 2c. Tính tổng chi phí từ tất cả hóa đơn và kiểm tra quỹ kho
        var totalCost = request.Invoices
            .Where(g => g.VatInvoice.TotalAmount.HasValue)
            .Sum(g => g.VatInvoice.TotalAmount!.Value);

        DepotFundModel? depotFund = null;
        if (totalCost > 0)
        {
            depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(depotId.Value, cancellationToken);

            // Pre-check quỹ/hạn mức tự ứng trước khi ghi bất kỳ dữ liệu nhập hàng nào xuống DB.
            // Dùng bản sao domain model để validate, không mutate trạng thái thật tại thời điểm này.
            var fundCheck = DepotFundModel.Reconstitute(
                depotFund.Id,
                depotFund.DepotId,
                depotFund.Balance,
                depotFund.MaxAdvanceLimit,
                depotFund.LastUpdatedAt);

            fundCheck.Debit(totalCost);
        }

        // 3. Guard trùng serial+number ngay trong cùng request
        var seenInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < request.Invoices.Count; i++)
            {
                var group = request.Invoices[i];
                var groupResult = new ImportPurchaseGroupResultDto { GroupIndex = i };
                var batchNote = NormalizeNote(group.BatchNote);

                // 3a. Kiểm tra trùng hóa đơn VAT
                var vat = group.VatInvoice;
                if (!string.IsNullOrWhiteSpace(vat.InvoiceSerial) && !string.IsNullOrWhiteSpace(vat.InvoiceNumber))
                {
                    var key = $"{vat.InvoiceSerial.Trim()}|{vat.InvoiceNumber.Trim()}";

                    if (!seenInvoices.Add(key))
                    {
                        throw new ConflictException(
                            $"Nhóm {i + 1}: Hóa đơn VAT ký hiệu '{vat.InvoiceSerial}' số '{vat.InvoiceNumber}' bị trùng lặp trong cùng yêu cầu nhập hàng.");
                    }

                    var isDuplicate = await _purchasedInventoryRepository.ExistsBySerialAndNumberAsync(
                        vat.InvoiceSerial.Trim(), vat.InvoiceNumber.Trim(), cancellationToken);
                    if (isDuplicate)
                    {
                        throw new ConflictException(
                            $"Nhóm {i + 1}: Hóa đơn VAT ký hiệu '{vat.InvoiceSerial}' số '{vat.InvoiceNumber}' đã tồn tại trong hệ thống.");
                    }
                }

                // 3b. Validate từng vật phẩm trong nhóm (dual-path)
                var validItems = new List<(ImportPurchasedItemDto dto, ItemModelRecord itemModel)>();
                var rowErrors = new Dictionary<int, HashSet<string>>();

                foreach (var item in group.Items)
                {
                    try
                    {
                        ItemModelRecord? resolvedRecord = null;

                        if (item.ItemModelId.HasValue)
                        {
                            // ── Path A: Existing item by ID ──
                            if (!existingItemModels.TryGetValue(item.ItemModelId.Value, out var existingRecord))
                            {
                                AddRowError(rowErrors, item.Row, $"Không tìm thấy item model có ID: {item.ItemModelId.Value}");
                                continue;
                            }
                            resolvedRecord = existingRecord;
                        }
                        else
                        {
                            // ── Path B: Create new item from metadata ──
                            var normalizedName = item.ItemName?.Trim();
                            var normalizedUnit = item.Unit?.Trim();
                            var normalizedItemType = item.ItemType?.Trim();
                            var normalizedCategoryCode = item.CategoryCode?.Trim();

                            if (string.IsNullOrWhiteSpace(normalizedName))
                            {
                                AddRowError(rowErrors, item.Row, "Tên vật phẩm không được để trống");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedCategoryCode))
                            {
                                AddRowError(rowErrors, item.Row, "Mã danh mục không được để trống");
                                continue;
                            }

                            var category = categoriesByCode.GetValueOrDefault(normalizedCategoryCode!);

                            if (category == null)
                            {
                                AddRowError(rowErrors, item.Row, $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedUnit))
                            {
                                AddRowError(rowErrors, item.Row, "Đơn vị tính không được để trống");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedItemType))
                            {
                                AddRowError(rowErrors, item.Row, "Loại vật phẩm không được để trống");
                                continue;
                            }

                            var targetGroups = item.TargetGroups?
                                .Where(g => !string.IsNullOrWhiteSpace(g))
                                .Select(g => g.Trim())
                                .ToList() ?? new();

                            if (targetGroups.Count == 0)
                            {
                                AddRowError(rowErrors, item.Row, "Nhóm đối tượng không được để trống");
                                continue;
                            }

                            try
                            {
                                resolvedRecord = ItemModelRecord.Create(
                                    category.Id,
                                    normalizedName,
                                    normalizedUnit,
                                    normalizedItemType,
                                    targetGroups,
                                    item.Description);
                                resolvedRecord.ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unexpected error creating ItemModelRecord for row {Row} group {GroupIndex}", item.Row, i);
                                AddRowError(rowErrors, item.Row, "Lỗi hệ thống khi tạo item model");
                                continue;
                            }
                        }

                        validItems.Add((item, resolvedRecord));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error processing item at row {Row} group {GroupIndex}", item.Row, i);
                        AddRowError(rowErrors, item.Row, ex.Message);
                    }
                }

                // Flatten row errors into sorted error list for this group
                var errors = rowErrors
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new ImportPurchasedErrorDto { Row = kv.Key, Message = $"[Dòng {kv.Key}] {string.Join("; ", kv.Value)}" })
                    .ToList();

                groupResult.Failed = errors.Count;
                groupResult.Errors = errors;

                if (validItems.Count == 0)
                {
                    response.Groups.Add(groupResult);
                    response.TotalFailed += errors.Count;
                    continue;
                }

                // Sort resolved items by row for predictable output
                validItems = validItems.OrderBy(x => x.dto.Row).ToList();

                // 3c. Validate CampaignDisbursementId trước khi ghi bất kỳ dữ liệu nào
                CampaignDisbursementModel? linkedDisbursement = null;
                if (group.CampaignDisbursementId.HasValue)
                {
                    linkedDisbursement = await _disbursementRepo.GetByIdAsync(group.CampaignDisbursementId.Value, cancellationToken)
                        ?? throw new NotFoundException($"Không tìm thấy giải ngân #{group.CampaignDisbursementId.Value}.");

                    if (linkedDisbursement.DepotId != depotId.Value)
                        throw new ForbiddenException("Giải ngân này không thuộc kho của bạn.");
                }

                // 4. Tạo hóa đơn VAT cho nhóm này
                var vatInvoiceModel = VatInvoiceModel.Create(
                    vat.InvoiceSerial,
                    vat.InvoiceNumber,
                    vat.SupplierName,
                    vat.SupplierTaxCode,
                    vat.InvoiceDate,
                    vat.TotalAmount,
                    vat.FileUrl);

                var savedVatInvoice = await _purchasedInventoryRepository.CreateVatInvoiceAsync(vatInvoiceModel, cancellationToken);

                // 5. Name-path rows: always create new item models. ID-path rows: use existing ID and ignore lookup fields.
                var newItemModels = validItems
                    .Where(x => !x.dto.ItemModelId.HasValue)
                    .Select(x => x.itemModel)
                    .ToList();
                var createdItems = await _purchasedInventoryRepository.CreateReliefItemsBulkAsync(newItemModels, cancellationToken);
                var createdIndex = 0;

                // 6. Map lại ItemModelId và tạo PurchasedInventoryItemModel
                var purchasedModels = new List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)>();
                foreach (var (dto, reliefItem) in validItems)
                {
                    var resolvedItemModelId = dto.ItemModelId ?? createdItems[createdIndex++].Id;

                    // Normalize dates to UTC before persisting
                    var receivedDateUtc = dto.ReceivedDate.HasValue
                        ? DateTime.SpecifyKind(dto.ReceivedDate.Value, DateTimeKind.Utc)
                        : (DateTime?)null;
                    var expiredDateUtc = dto.ExpiredDate.HasValue
                        ? DateTime.SpecifyKind(dto.ExpiredDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                        : (DateTime?)null;

                    var purchasedModel = PurchasedInventoryItemModel.Create(
                        savedVatInvoice.Id,
                        resolvedItemModelId,
                        dto.Quantity,
                        receivedDateUtc,
                        expiredDateUtc,
                        batchNote,
                        request.UserId,
                        depotId.Value,
                        batchNote,
                        null);

                    purchasedModels.Add((purchasedModel, dto.UnitPrice, reliefItem.ItemType));
                }

                // 7. Bulk insert — kiểm tra sức chứa kho và lưu inventory log
                await _purchasedInventoryRepository.AddPurchasedInventoryItemsBulkAsync(purchasedModels, cancellationToken);

                // 7b. Tự động ghi vào bảng công khai disbursement_items nếu nhóm liên kết với CampaignDisbursement
                //     linkedDisbursement đã được validate ở step 3c — không query lại DB
                if (linkedDisbursement != null)
                {
                    var disbursementItems = validItems.Select(x =>
                    {
                        // Use resolved record Name/Unit for disbursement snapshot (not raw DTO which may be null for Path A)
                        var resolvedName = !string.IsNullOrWhiteSpace(x.dto.ItemName) ? x.dto.ItemName.Trim() : x.itemModel.Name;
                        var resolvedUnit = !string.IsNullOrWhiteSpace(x.dto.Unit) ? x.dto.Unit.Trim() : x.itemModel.Unit;

                        return new DisbursementItemModel
                        {
                            CampaignDisbursementId = linkedDisbursement.Id,
                            ItemName      = resolvedName,
                            Unit          = resolvedUnit,
                            Quantity      = x.dto.Quantity,
                            UnitPrice     = x.dto.UnitPrice ?? 0m,
                            TotalPrice    = (x.dto.UnitPrice ?? 0m) * x.dto.Quantity,
                            Note          = batchNote,
                            CreatedAt     = DateTime.UtcNow
                        };
                    }).ToList();

                    await _disbursementRepo.AddItemsAsync(linkedDisbursement.Id, disbursementItems, cancellationToken);
                    groupResult.DisbursementItemsLogged = disbursementItems.Count;
                }

                groupResult.VatInvoiceId = savedVatInvoice.Id;
                groupResult.Imported = purchasedModels.Count;

                response.Groups.Add(groupResult);
                response.TotalImported += purchasedModels.Count;
                response.TotalFailed += errors.Count;
            }

            // 8b. Trừ quỹ kho dựa trên tổng chi phí hóa đơn (cho phép balance âm nếu trong hạn mức tự ứng)
            if (totalCost > 0 && depotFund != null)
            {
                var debitResult = depotFund.Debit(totalCost);
                await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

                var depotName = depotFund.DepotName ?? $"Kho #{depotFund.DepotId}";

                if (debitResult.IsAdvanced)
                {
                    // Kho tự ứng — ghi transaction SelfAdvance
                    await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                    {
                        DepotFundId = depotFund.Id,
                        TransactionType = DepotFundTransactionType.SelfAdvance,
                        Amount = totalCost,
                        ReferenceType = "VatInvoice",
                        ReferenceId = null,
                        Note = $"{depotName} đã tự ứng {debitResult.AdvancedAmount:N0} VNĐ để nhập vật tư ({request.Invoices.Count} hóa đơn, tổng {totalCost:N0} VNĐ)",
                        CreatedBy = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                else
                {
                    // Đủ quỹ — ghi transaction Deduction bình thường
                    await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                    {
                        DepotFundId = depotFund.Id,
                        TransactionType = DepotFundTransactionType.Deduction,
                        Amount = totalCost,
                        ReferenceType = "VatInvoice",
                        ReferenceId = null,
                        Note = $"Nhập hàng {request.Invoices.Count} hóa đơn, tổng {totalCost:N0} VNĐ",
                        CreatedBy = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            // 9. Commit tất cả các nhóm trong 1 transaction
            await _unitOfWork.SaveChangesWithTransactionAsync();
        }
        catch (DomainException)
        {
            throw;
        }
        catch (ConflictException)
        {
            throw;
        }
        catch (NotFoundException)
        {
            throw;
        }
        catch (ForbiddenException)
        {
            throw;
        }
        catch (BadRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình nhập hàng có VAT. Tất cả thay đổi đã được hoàn tác.");
            throw new CreateFailedException("Lỗi trong quá trình nhập hàng. Vui lòng thử lại.");
        }

        return response;
    }

    /// <summary>
    /// Adds an error message for a specific row. Deduplicates via HashSet.
    /// </summary>
    private static void AddRowError(Dictionary<int, HashSet<string>> rowErrors, int row, string message)
    {
        if (!rowErrors.TryGetValue(row, out var messages))
        {
            messages = new HashSet<string>(StringComparer.Ordinal);
            rowErrors[row] = messages;
        }
        messages.Add(message);
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
