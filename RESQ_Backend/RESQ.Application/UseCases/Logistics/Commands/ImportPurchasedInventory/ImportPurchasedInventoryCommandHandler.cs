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
    IManagerDepotAccessService managerDepotAccessService,
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
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IPurchasedInventoryRepository _purchasedInventoryRepository = purchasedInventoryRepository;
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

        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);
        if (depotId == null)
        {
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động. Không thể nhập hàng.");
        }

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId.Value, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
        {
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể nhập hàng vào kho này.");
        }

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var categoriesByCode = categories
            .ToDictionary(c => c.Code.ToString(), c => c, StringComparer.OrdinalIgnoreCase);

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
            existingItemModels = [];
        }

        DepotFundModel? selectedDepotFund = null;
        if (request.DepotFundId.HasValue)
        {
            selectedDepotFund = await _depotFundRepo.GetByIdAsync(request.DepotFundId.Value, cancellationToken)
                ?? throw new BadRequestException($"Không tìm thấy quỹ kho #{request.DepotFundId.Value}.");

            if (selectedDepotFund.DepotId != depotId.Value)
            {
                throw new ForbiddenException("Quỹ này không thuộc kho của bạn.");
            }
        }

        var seenInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successfulInvoiceCharges = new List<(int VatInvoiceId, decimal Amount, string? InvoiceSerial, string? InvoiceNumber)>();

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                for (var groupIndex = 0; groupIndex < request.Invoices.Count; groupIndex++)
                {
                    var group = request.Invoices[groupIndex];
                    var groupResult = new ImportPurchaseGroupResultDto { GroupIndex = groupIndex };
                    var batchNote = NormalizeNote(group.BatchNote);
                    var vat = group.VatInvoice;

                    if (!string.IsNullOrWhiteSpace(vat.InvoiceSerial) && !string.IsNullOrWhiteSpace(vat.InvoiceNumber))
                    {
                        var invoiceKey = $"{vat.InvoiceSerial.Trim()}|{vat.InvoiceNumber.Trim()}";
                        if (!seenInvoices.Add(invoiceKey))
                        {
                            throw new ConflictException(
                                $"Nhóm {groupIndex + 1}: Hóa đơn VAT ký hiệu '{vat.InvoiceSerial}' số '{vat.InvoiceNumber}' bị trùng lặp trong cùng yêu cầu nhập hàng.");
                        }

                        var isDuplicate = await _purchasedInventoryRepository.ExistsBySerialAndNumberAsync(
                            vat.InvoiceSerial.Trim(),
                            vat.InvoiceNumber.Trim(),
                            cancellationToken);
                        if (isDuplicate)
                        {
                            throw new ConflictException(
                                $"Nhóm {groupIndex + 1}: Hóa đơn VAT ký hiệu '{vat.InvoiceSerial}' số '{vat.InvoiceNumber}' đã tồn tại trong hệ thống.");
                        }
                    }

                    var validItems = new List<(ImportPurchasedItemDto dto, ItemModelRecord itemModel)>();
                    var rowErrors = new Dictionary<int, HashSet<string>>();

                    foreach (var item in group.Items)
                    {
                        try
                        {
                            ItemModelRecord? resolvedRecord = null;

                            if (item.ItemModelId.HasValue)
                            {
                                if (!existingItemModels.TryGetValue(item.ItemModelId.Value, out var existingRecord))
                                {
                                    AddRowError(rowErrors, item.Row, $"Không tìm thấy item model có ID: {item.ItemModelId.Value}");
                                    continue;
                                }

                                resolvedRecord = existingRecord;
                            }
                            else
                            {
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
                                    .ToList() ?? [];

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
                                        volumePerUnit: item.VolumePerUnit ?? 0,
                                        weightPerUnit: item.WeightPerUnit ?? 0,
                                        description: item.Description);
                                    resolvedRecord.ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Unexpected error creating ItemModelRecord for row {Row} group {GroupIndex}", item.Row, groupIndex);
                                    AddRowError(rowErrors, item.Row, "Lỗi hệ thống khi tạo item model");
                                    continue;
                                }
                            }

                            validItems.Add((item, resolvedRecord));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error processing item at row {Row} group {GroupIndex}", item.Row, groupIndex);
                            AddRowError(rowErrors, item.Row, ex.Message);
                        }
                    }

                    var errors = rowErrors
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new ImportPurchasedErrorDto
                        {
                            Row = kv.Key,
                            Message = $"[Dòng {kv.Key}] {string.Join("; ", kv.Value)}"
                        })
                        .ToList();

                    groupResult.Failed = errors.Count;
                    groupResult.Errors = errors;

                    if (validItems.Count == 0)
                    {
                        response.Groups.Add(groupResult);
                        response.TotalFailed += errors.Count;
                        continue;
                    }

                    validItems = validItems.OrderBy(x => x.dto.Row).ToList();

                    CampaignDisbursementModel? linkedDisbursement = null;
                    if (group.CampaignDisbursementId.HasValue)
                    {
                        linkedDisbursement = await _disbursementRepo.GetByIdAsync(group.CampaignDisbursementId.Value, cancellationToken)
                            ?? throw new NotFoundException($"Không tìm thấy giải ngân #{group.CampaignDisbursementId.Value}.");

                        if (linkedDisbursement.DepotId != depotId.Value)
                        {
                            throw new ForbiddenException("Giải ngân này không thuộc kho của bạn.");
                        }
                    }

                    var stagedVatInvoice = await _purchasedInventoryRepository.CreateVatInvoiceAsync(
                        VatInvoiceModel.Create(
                            vat.InvoiceSerial,
                            vat.InvoiceNumber,
                            vat.SupplierName,
                            vat.SupplierTaxCode,
                            vat.InvoiceDate,
                            vat.TotalAmount,
                            vat.FileUrl),
                        cancellationToken);

                    var newItemModels = validItems
                        .Where(x => !x.dto.ItemModelId.HasValue)
                        .Select(x => x.itemModel)
                        .ToList();
                    var createdItemReferences = await _purchasedInventoryRepository.CreateReliefItemsBulkAsync(newItemModels, cancellationToken);

                    // Flush trong transaction để lấy ID thật cho VAT invoice và item model mới.
                    await _unitOfWork.SaveAsync();

                    var purchasedModels = new List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)>();
                    var createdIndex = 0;
                    foreach (var (dto, resolvedItemModel) in validItems)
                    {
                        var resolvedItemModelId = dto.ItemModelId ?? createdItemReferences[createdIndex++].CurrentId;

                        var receivedDateUtc = dto.ReceivedDate.HasValue
                            ? DateTime.SpecifyKind(dto.ReceivedDate.Value, DateTimeKind.Utc)
                            : (DateTime?)null;
                        var expiredDateUtc = dto.ExpiredDate.HasValue
                            ? DateTime.SpecifyKind(dto.ExpiredDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                            : (DateTime?)null;

                        var purchasedModel = PurchasedInventoryItemModel.Create(
                            stagedVatInvoice.CurrentId,
                            resolvedItemModelId,
                            dto.Quantity,
                            receivedDateUtc,
                            expiredDateUtc,
                            batchNote,
                            request.UserId,
                            depotId.Value,
                            batchNote,
                            null);

                        purchasedModels.Add((purchasedModel, dto.UnitPrice, resolvedItemModel.ItemType));
                    }

                    await _purchasedInventoryRepository.AddPurchasedInventoryItemsBulkAsync(purchasedModels, cancellationToken);

                    if (linkedDisbursement != null)
                    {
                        var disbursementItems = validItems.Select(x =>
                        {
                            var resolvedName = !string.IsNullOrWhiteSpace(x.dto.ItemName) ? x.dto.ItemName.Trim() : x.itemModel.Name;
                            var resolvedUnit = !string.IsNullOrWhiteSpace(x.dto.Unit) ? x.dto.Unit.Trim() : x.itemModel.Unit;

                            return new DisbursementItemModel
                            {
                                CampaignDisbursementId = linkedDisbursement.Id,
                                ItemName = resolvedName,
                                Unit = resolvedUnit,
                                Quantity = x.dto.Quantity,
                                UnitPrice = x.dto.UnitPrice ?? 0m,
                                TotalPrice = (x.dto.UnitPrice ?? 0m) * x.dto.Quantity,
                                Note = batchNote,
                                CreatedAt = DateTime.UtcNow
                            };
                        }).ToList();

                        await _disbursementRepo.AddItemsAsync(linkedDisbursement.Id, disbursementItems, cancellationToken);
                        groupResult.DisbursementItemsLogged = disbursementItems.Count;
                    }

                    // Flush để persist toàn bộ inventory/lot/reusable/log của nhóm trước khi sang nhóm tiếp theo.
                    await _unitOfWork.SaveAsync();

                    groupResult.VatInvoiceId = stagedVatInvoice.CurrentId;
                    groupResult.Imported = purchasedModels.Count;

                    if (vat.TotalAmount.HasValue && vat.TotalAmount.Value > 0)
                    {
                        successfulInvoiceCharges.Add((
                            stagedVatInvoice.CurrentId,
                            vat.TotalAmount.Value,
                            vat.InvoiceSerial?.Trim(),
                            vat.InvoiceNumber?.Trim()));
                    }

                    response.Groups.Add(groupResult);
                    response.TotalImported += purchasedModels.Count;
                    response.TotalFailed += errors.Count;
                }

                var totalChargedAmount = successfulInvoiceCharges.Sum(charge => charge.Amount);
                if (totalChargedAmount > 0)
                {
                    var depotFund = selectedDepotFund
                        ?? await ResolveDepotFundForPurchaseImportAsync(depotId.Value, cancellationToken);

                    depotFund.Debit(totalChargedAmount);
                    await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

                    foreach (var charge in successfulInvoiceCharges)
                    {
                        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                        {
                            DepotFundId = depotFund.Id,
                            TransactionType = DepotFundTransactionType.Deduction,
                            Amount = charge.Amount,
                            ReferenceType = DepotFundReferenceType.VatInvoice.ToString(),
                            ReferenceId = charge.VatInvoiceId,
                            Note = BuildVatInvoiceTransactionNote(charge.InvoiceSerial, charge.InvoiceNumber, charge.Amount),
                            CreatedBy = request.UserId,
                            CreatedAt = DateTime.UtcNow
                        }, cancellationToken);
                    }
                }

                await _unitOfWork.SaveAsync();
            });

            try
            {
                var depot = await _depotRepository.GetByIdAsync(depotId.Value, cancellationToken);
                var depotName = depot?.Name ?? $"Kho #{depotId.Value}";
                var coordinatorIds = await _userRepository.GetActiveCoordinatorUserIdsAsync(cancellationToken);
                var notifTitle = "Thông báo nhập hàng mới";
                var notifBody = $"Kho {depotName} vừa hoàn tất nhập hàng mua sắm ({response.TotalImported} mặt hàng). Đề nghị kiểm tra và xác nhận tình trạng tồn kho.";
                var notifData = new Dictionary<string, string>
                {
                    ["depotId"] = depotId.Value.ToString(),
                    ["type"] = "depot_purchase_imported"
                };
                foreach (var coordinatorId in coordinatorIds)
                {
                    _ = _firebaseService.SendNotificationToUserAsync(
                        coordinatorId,
                        notifTitle,
                        notifBody,
                        "depot_purchase_imported",
                        notifData,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify coordinators after purchase import | DepotId={DepotId}", depotId.Value);
            }
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

    private static void AddRowError(Dictionary<int, HashSet<string>> rowErrors, int row, string message)
    {
        if (!rowErrors.TryGetValue(row, out var messages))
        {
            messages = new HashSet<string>(StringComparer.Ordinal);
            rowErrors[row] = messages;
        }

        messages.Add(message);
    }

    private static string BuildVatInvoiceTransactionNote(string? invoiceSerial, string? invoiceNumber, decimal amount)
    {
        var invoiceLabel = !string.IsNullOrWhiteSpace(invoiceSerial) && !string.IsNullOrWhiteSpace(invoiceNumber)
            ? $"hóa đơn VAT ký hiệu {invoiceSerial} số {invoiceNumber}"
            : "hóa đơn VAT";

        return $"Thanh toán nhập hàng theo {invoiceLabel} - {amount:N0} VNĐ";
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private async Task<DepotFundModel> ResolveDepotFundForPurchaseImportAsync(int depotId, CancellationToken cancellationToken)
    {
        var depotFunds = await _depotFundRepo.GetAllByDepotIdAsync(depotId, cancellationToken);

        if (depotFunds.Count == 0)
        {
            throw new BadRequestException("Kho hiện chưa có quỹ hợp lệ để thanh toán nhập mua. Vui lòng tạo hoặc cấp quỹ trước khi nhập hàng.");
        }

        if (depotFunds.Count > 1)
        {
            throw new BadRequestException("Kho hiện có nhiều quỹ. Vui lòng truyền rõ DepotFundId để chọn đúng quỹ cần thanh toán.");
        }

        return depotFunds[0];
    }
}
