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
    IUnitOfWork unitOfWork,
    ILogger<ImportPurchasedInventoryCommandHandler> logger)
    : IRequestHandler<ImportPurchasedInventoryCommand, ImportPurchasedInventoryResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IPurchasedInventoryRepository _purchasedInventoryRepository = purchasedInventoryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ICampaignDisbursementRepository _disbursementRepo = campaignDisbursementRepository;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepository;
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

        // 2b. Tính tổng chi phí từ tất cả hóa đơn và kiểm tra quỹ kho
        var totalCost = request.Invoices
            .Where(g => g.VatInvoice.TotalAmount.HasValue)
            .Sum(g => g.VatInvoice.TotalAmount!.Value);

        DepotFundModel? depotFund = null;
        if (totalCost > 0)
        {
            depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(depotId.Value, cancellationToken);
            if (depotFund.Balance < totalCost)
            {
                throw new BadRequestException(
                    $"Quỹ kho không đủ. Số dư hiện tại: {depotFund.Balance:N0} VNĐ, tổng chi phí nhập hàng: {totalCost:N0} VNĐ.");
            }
        }

        // 3. Guard trùng serial+number ngay trong cùng request
        var seenInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < request.Invoices.Count; i++)
            {
                var group = request.Invoices[i];
                var groupResult = new ImportPurchaseGroupResultDto { GroupIndex = i };

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

                // 3b. Validate từng vật phẩm trong nhóm
                var validItems = new List<(ImportPurchasedItemDto dto, ItemModelRecord itemModel)>();
                var errors = new List<ImportPurchasedErrorDto>();

                foreach (var item in group.Items)
                {
                    try
                    {
                        var category = categories.FirstOrDefault(c =>
                            string.Equals(c.Code.ToString(), item.CategoryCode, StringComparison.OrdinalIgnoreCase));

                        if (category == null)
                        {
                            errors.Add(new ImportPurchasedErrorDto
                            {
                                Row = item.Row,
                                Message = $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}"
                            });
                            continue;
                        }

                        var reliefItemModel = ItemModelRecord.Create(
                            category.Id,
                            item.ItemName,
                            item.Unit,
                            item.ItemType,
                            item.TargetGroup);

                        validItems.Add((item, reliefItemModel));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi validate vật phẩm dòng {Row} nhóm {GroupIndex}", item.Row, i);
                        errors.Add(new ImportPurchasedErrorDto { Row = item.Row, Message = ex.Message });
                    }
                }

                groupResult.Failed = errors.Count;
                groupResult.Errors = errors;

                if (validItems.Count == 0)
                {
                    response.Groups.Add(groupResult);
                    response.TotalFailed += errors.Count;
                    continue;
                }

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

                // 5. Bulk lấy/tạo relief items cho nhóm này
                var reliefItemModels = validItems.Select(x => x.itemModel).ToList();
                var savedReliefItems = await _purchasedInventoryRepository.GetOrCreateReliefItemsBulkAsync(reliefItemModels, cancellationToken);

                // 6. Map lại ItemModelId và tạo PurchasedInventoryItemModel
                var purchasedModels = new List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)>();
                foreach (var (dto, reliefItem) in validItems)
                {
                    var savedReliefItem = savedReliefItems.FirstOrDefault(r =>
                        r.Name == reliefItem.Name &&
                        r.CategoryId == reliefItem.CategoryId &&
                        r.Unit == reliefItem.Unit &&
                        r.ItemType == reliefItem.ItemType &&
                        r.TargetGroup == reliefItem.TargetGroup);

                    if (savedReliefItem != null)
                    {
                        // Normalize dates to UTC before persisting
                        var receivedDateUtc = dto.ReceivedDate.HasValue
                            ? DateTime.SpecifyKind(dto.ReceivedDate.Value, DateTimeKind.Utc)
                            : (DateTime?)null;
                        var expiredDateUtc = dto.ExpiredDate.HasValue
                            ? DateTime.SpecifyKind(dto.ExpiredDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                            : (DateTime?)null;

                        var purchasedModel = PurchasedInventoryItemModel.Create(
                            savedVatInvoice.Id,
                            savedReliefItem.Id,
                            dto.Quantity,
                            receivedDateUtc,
                            expiredDateUtc,
                            dto.Notes,
                            request.UserId,
                            depotId.Value);

                        purchasedModels.Add((purchasedModel, dto.UnitPrice, savedReliefItem.ItemType));
                    }
                }

                // 7. Bulk insert — kiểm tra sức chứa kho và lưu inventory log
                await _purchasedInventoryRepository.AddPurchasedInventoryItemsBulkAsync(purchasedModels, cancellationToken);

                // 7b. Tự động ghi vào bảng công khai disbursement_items nếu nhóm liên kết với CampaignDisbursement
                //     linkedDisbursement đã được validate ở step 3c — không query lại DB
                if (linkedDisbursement != null)
                {
                    var disbursementItems = validItems.Select(x => new DisbursementItemModel
                    {
                        CampaignDisbursementId = linkedDisbursement.Id,
                        ItemName      = x.dto.ItemName,
                        Unit          = x.dto.Unit,
                        Quantity      = x.dto.Quantity,
                        UnitPrice     = x.dto.UnitPrice ?? 0m,
                        TotalPrice    = (x.dto.UnitPrice ?? 0m) * x.dto.Quantity,
                        Note          = x.dto.Notes,
                        CreatedAt     = DateTime.UtcNow
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

            // 8b. Trừ quỹ kho dựa trên tổng chi phí hóa đơn
            if (totalCost > 0 && depotFund != null)
            {
                depotFund.Debit(totalCost);
                await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

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
}
