using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandHandler(
    IItemCategoryRepository categoryRepository,
    IPurchasedInventoryRepository purchasedInventoryRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    ICampaignDisbursementRepository campaignDisbursementRepository,
    IDepotFundRepository depotFundRepository,
    IUserRepository userRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IUnitOfWork unitOfWork,
    IFirebaseService firebaseService,
    ILogger<ImportPurchasedInventoryCommandHandler> logger)
    : IRequestHandler<ImportPurchasedInventoryCommand, ImportPurchasedInventoryResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IPurchasedInventoryRepository _purchasedInventoryRepository = purchasedInventoryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ICampaignDisbursementRepository _disbursementRepo = campaignDisbursementRepository;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<ImportPurchasedInventoryCommandHandler> _logger = logger;

    public async Task<ImportPurchasedInventoryResponse> Handle(ImportPurchasedInventoryCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportPurchasedInventoryResponse();

        // 1. Láº¥y kho Ä‘ang hoáº¡t Ä‘á»™ng mÃ  ngÆ°á»i dÃ¹ng quáº£n lÃ½
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken);
        if (depotId == null)
        {
            throw new BadRequestException("TÃ i khoáº£n hiá»‡n táº¡i khÃ´ng Ä‘Æ°á»£c chá»‰ Ä‘á»‹nh quáº£n lÃ½ báº¥t ká»³ kho nÃ o Ä‘ang hoáº¡t Ä‘á»™ng. KhÃ´ng thá»ƒ nháº­p hÃ ng.");
        }
        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId.Value, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể nhập hàng vào kho này.");
        // 2. Táº£i táº¥t cáº£ danh má»¥c Ä‘á»ƒ mapping hiá»‡u quáº£
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

        // 2c. TÃ­nh tá»•ng chi phÃ­ tá»« táº¥t cáº£ hÃ³a Ä‘Æ¡n vÃ  kiá»ƒm tra quá»¹ kho
        var totalCost = request.Invoices
            .Where(g => g.VatInvoice.TotalAmount.HasValue)
            .Sum(g => g.VatInvoice.TotalAmount!.Value);

        DepotFundModel? depotFund = null;
        if (totalCost > 0)
        {
            // Nếu manager chọn quỹ cụ thể → dùng quỹ đó; ngược lại → legacy behavior
            if (request.DepotFundId.HasValue)
            {
                depotFund = await _depotFundRepo.GetByIdAsync(request.DepotFundId.Value, cancellationToken)
                    ?? throw new BadRequestException($"Không tìm thấy quỹ kho #{request.DepotFundId.Value}.");
                if (depotFund.DepotId != depotId.Value)
                    throw new ForbiddenException("Quỹ này không thuộc kho của bạn.");
            }
            else
            {
                depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(depotId.Value, cancellationToken);
            }

            var fundCheck = DepotFundModel.Reconstitute(
                depotFund.Id,
                depotFund.DepotId,
                depotFund.Balance,
                depotFund.AdvanceLimit,
                depotFund.OutstandingAdvanceAmount,
                depotFund.LastUpdatedAt,
                depotFund.FundSourceType,
                depotFund.FundSourceId);

            fundCheck.Debit(totalCost);
        }

        // 3. Guard trÃ¹ng serial+number ngay trong cÃ¹ng request
        var seenInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < request.Invoices.Count; i++)
            {
                var group = request.Invoices[i];
                var groupResult = new ImportPurchaseGroupResultDto { GroupIndex = i };
                var batchNote = NormalizeNote(group.BatchNote);

                // 3a. Kiá»ƒm tra trÃ¹ng hÃ³a Ä‘Æ¡n VAT
                var vat = group.VatInvoice;
                if (!string.IsNullOrWhiteSpace(vat.InvoiceSerial) && !string.IsNullOrWhiteSpace(vat.InvoiceNumber))
                {
                    var key = $"{vat.InvoiceSerial.Trim()}|{vat.InvoiceNumber.Trim()}";

                    if (!seenInvoices.Add(key))
                    {
                        throw new ConflictException(
                            $"NhÃ³m {i + 1}: HÃ³a Ä‘Æ¡n VAT kÃ½ hiá»‡u '{vat.InvoiceSerial}' sá»‘ '{vat.InvoiceNumber}' bá»‹ trÃ¹ng láº·p trong cÃ¹ng yÃªu cáº§u nháº­p hÃ ng.");
                    }

                    var isDuplicate = await _purchasedInventoryRepository.ExistsBySerialAndNumberAsync(
                        vat.InvoiceSerial.Trim(), vat.InvoiceNumber.Trim(), cancellationToken);
                    if (isDuplicate)
                    {
                        throw new ConflictException(
                            $"NhÃ³m {i + 1}: HÃ³a Ä‘Æ¡n VAT kÃ½ hiá»‡u '{vat.InvoiceSerial}' sá»‘ '{vat.InvoiceNumber}' Ä‘Ã£ tá»“n táº¡i trong há»‡ thá»‘ng.");
                    }
                }

                // 3b. Validate tá»«ng váº­t pháº©m trong nhÃ³m (dual-path)
                var validItems = new List<(ImportPurchasedItemDto dto, ItemModelRecord itemModel)>();
                var rowErrors = new Dictionary<int, HashSet<string>>();

                foreach (var item in group.Items)
                {
                    try
                    {
                        ItemModelRecord? resolvedRecord = null;

                        if (item.ItemModelId.HasValue)
                        {
                            // â”€â”€ Path A: Existing item by ID â”€â”€
                            if (!existingItemModels.TryGetValue(item.ItemModelId.Value, out var existingRecord))
                            {
                                AddRowError(rowErrors, item.Row, $"KhÃ´ng tÃ¬m tháº¥y item model cÃ³ ID: {item.ItemModelId.Value}");
                                continue;
                            }
                            resolvedRecord = existingRecord;
                        }
                        else
                        {
                            // â”€â”€ Path B: Create new item from metadata â”€â”€
                            var normalizedName = item.ItemName?.Trim();
                            var normalizedUnit = item.Unit?.Trim();
                            var normalizedItemType = item.ItemType?.Trim();
                            var normalizedCategoryCode = item.CategoryCode?.Trim();

                            if (string.IsNullOrWhiteSpace(normalizedName))
                            {
                                AddRowError(rowErrors, item.Row, "TÃªn váº­t pháº©m khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedCategoryCode))
                            {
                                AddRowError(rowErrors, item.Row, "MÃ£ danh má»¥c khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
                                continue;
                            }

                            var category = categoriesByCode.GetValueOrDefault(normalizedCategoryCode!);

                            if (category == null)
                            {
                                AddRowError(rowErrors, item.Row, $"KhÃ´ng tÃ¬m tháº¥y danh má»¥c váº­t pháº©m cÃ³ mÃ£: {item.CategoryCode}");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedUnit))
                            {
                                AddRowError(rowErrors, item.Row, "ÄÆ¡n vá»‹ tÃ­nh khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(normalizedItemType))
                            {
                                AddRowError(rowErrors, item.Row, "Loáº¡i váº­t pháº©m khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
                                continue;
                            }

                            var targetGroups = item.TargetGroups?
                                .Where(g => !string.IsNullOrWhiteSpace(g))
                                .Select(g => g.Trim())
                                .ToList() ?? new();

                            if (targetGroups.Count == 0)
                            {
                                AddRowError(rowErrors, item.Row, "NhÃ³m Ä‘á»‘i tÆ°á»£ng khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
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
                                    volumePerUnit: item.VolumePerUnit ?? 0,
                                    weightPerUnit: item.WeightPerUnit ?? 0,
                                    description: item.Description);
                                resolvedRecord.ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unexpected error creating ItemModelRecord for row {Row} group {GroupIndex}", item.Row, i);
                                AddRowError(rowErrors, item.Row, "Lá»—i há»‡ thá»‘ng khi táº¡o item model");
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
                    .Select(kv => new ImportPurchasedErrorDto { Row = kv.Key, Message = $"[DÃ²ng {kv.Key}] {string.Join("; ", kv.Value)}" })
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

                // 3c. Validate CampaignDisbursementId trÆ°á»›c khi ghi báº¥t ká»³ dá»¯ liá»‡u nÃ o
                CampaignDisbursementModel? linkedDisbursement = null;
                if (group.CampaignDisbursementId.HasValue)
                {
                    linkedDisbursement = await _disbursementRepo.GetByIdAsync(group.CampaignDisbursementId.Value, cancellationToken)
                        ?? throw new NotFoundException($"KhÃ´ng tÃ¬m tháº¥y giáº£i ngÃ¢n #{group.CampaignDisbursementId.Value}.");

                    if (linkedDisbursement.DepotId != depotId.Value)
                        throw new ForbiddenException("Giáº£i ngÃ¢n nÃ y khÃ´ng thuá»™c kho cá»§a báº¡n.");
                }

                // 4. Táº¡o hÃ³a Ä‘Æ¡n VAT cho nhÃ³m nÃ y
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

                // 6. Map láº¡i ItemModelId vÃ  táº¡o PurchasedInventoryItemModel
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

                // 7. Bulk insert â€” kiá»ƒm tra sá»©c chá»©a kho vÃ  lÆ°u inventory log
                await _purchasedInventoryRepository.AddPurchasedInventoryItemsBulkAsync(purchasedModels, cancellationToken);

                // 7b. Tá»± Ä‘á»™ng ghi vÃ o báº£ng cÃ´ng khai disbursement_items náº¿u nhÃ³m liÃªn káº¿t vá»›i CampaignDisbursement
                //     linkedDisbursement Ä‘Ã£ Ä‘Æ°á»£c validate á»Ÿ step 3c â€” khÃ´ng query láº¡i DB
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

            // 8b. Trừ quỹ kho dựa trên tổng chi phí hóa đơn (bây giờ không cho phép âm)
            if (totalCost > 0 && depotFund != null)
            {
                depotFund.Debit(totalCost);
                await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

                // Ghi transaction Deduction bình thường
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

            // 9. Commit táº¥t cáº£ cÃ¡c nhÃ³m trong 1 transaction
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
            _logger.LogError(ex, "Lá»—i trong quÃ¡ trÃ¬nh nháº­p hÃ ng cÃ³ VAT. Táº¥t cáº£ thay Ä‘á»•i Ä‘Ã£ Ä‘Æ°á»£c hoÃ n tÃ¡c.");
            throw new CreateFailedException("Lá»—i trong quÃ¡ trÃ¬nh nháº­p hÃ ng. Vui lÃ²ng thá»­ láº¡i.");
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


