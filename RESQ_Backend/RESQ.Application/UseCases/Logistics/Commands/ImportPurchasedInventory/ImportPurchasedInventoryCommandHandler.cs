using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommandHandler(
    IItemCategoryRepository categoryRepository,
    IPurchasedInventoryRepository purchasedInventoryRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<ImportPurchasedInventoryCommandHandler> logger)
    : IRequestHandler<ImportPurchasedInventoryCommand, ImportPurchasedInventoryResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IPurchasedInventoryRepository _purchasedInventoryRepository = purchasedInventoryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
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

        // 2. Business Rule: Guard duplicate VAT invoice (InvoiceSerial + InvoiceNumber must be unique)
        var vat = request.VatInvoice;
        if (!string.IsNullOrWhiteSpace(vat.InvoiceSerial) && !string.IsNullOrWhiteSpace(vat.InvoiceNumber))
        {
            var isDuplicate = await _purchasedInventoryRepository.ExistsBySerialAndNumberAsync(
                vat.InvoiceSerial.Trim(), vat.InvoiceNumber.Trim(), cancellationToken);
            if (isDuplicate)
            {
                throw new ConflictException($"Hóa đơn VAT với ký hiệu '{vat.InvoiceSerial}' và số '{vat.InvoiceNumber}' đã tồn tại trong hệ thống.");
            }
        }

        // 2. Tải tất cả danh mục để mapping hiệu quả
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        // 3. Validate từng item và chuẩn bị domain models
        var validItems = new List<(ImportPurchasedItemDto dto, ReliefItemModel reliefItem)>();
        var errors = new List<ImportPurchasedErrorDto>();

        foreach (var item in request.Items)
        {
            try
            {
                var category = categories.FirstOrDefault(c =>
                    string.Equals(c.Code.ToString(), item.CategoryCode, StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    errors.Add(new ImportPurchasedErrorDto { Row = item.Row, Message = $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}" });
                    continue;
                }

                var reliefItemModel = ReliefItemModel.Create(
                    category.Id,
                    item.ItemName,
                    item.Unit,
                    item.ItemType,
                    item.TargetGroup);

                validItems.Add((item, reliefItemModel));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi validate vật phẩm dòng {Row}", item.Row);
                errors.Add(new ImportPurchasedErrorDto { Row = item.Row, Message = ex.Message });
            }
        }

        response.Failed = errors.Count;
        response.Errors = errors;

        if (validItems.Count == 0)
        {
            return response;
        }

        // 4. Thực hiện tất cả thao tác bulk trong transaction để đảm bảo tính atomicity
        try
        {
            // Tạo hóa đơn VAT
            var vatInvoiceModel = VatInvoiceModel.Create(
                request.VatInvoice.InvoiceSerial,
                request.VatInvoice.InvoiceNumber,
                request.VatInvoice.SupplierName,
                request.VatInvoice.SupplierTaxCode,
                request.VatInvoice.InvoiceDate,
                request.VatInvoice.TotalAmount,
                request.VatInvoice.FileUrl);

            var savedVatInvoice = await _purchasedInventoryRepository.CreateVatInvoiceAsync(vatInvoiceModel, cancellationToken);

            // Bulk tạo/lấy relief items
            var reliefItemModels = validItems.Select(x => x.reliefItem).ToList();
            var savedReliefItems = await _purchasedInventoryRepository.GetOrCreateReliefItemsBulkAsync(reliefItemModels, cancellationToken);

            // Map lại ReliefItemId và tạo PurchasedInventoryItemModel
            var purchasedModels = new List<PurchasedInventoryItemModel>();
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
                    var purchasedModel = PurchasedInventoryItemModel.Create(
                        savedVatInvoice.Id,
                        savedReliefItem.Id,
                        dto.Quantity,
                        dto.UnitPrice,
                        dto.ReceivedDate,
                        dto.ExpiredDate,
                        dto.Notes,
                        request.UserId,
                        depotId.Value);

                    purchasedModels.Add(purchasedModel);
                }
            }

            // Bulk insert purchased items, cập nhật tồn kho và tạo inventory log
            await _purchasedInventoryRepository.AddPurchasedInventoryItemsBulkAsync(purchasedModels, cancellationToken);

            // Lưu toàn bộ trong transaction
            await _unitOfWork.SaveChangesWithTransactionAsync();

            response.Imported = purchasedModels.Count;
            response.VatInvoiceId = savedVatInvoice.Id;
        }
        catch (DomainException)
        {
            // Let DomainExceptionBehaviour convert this to HTTP 400 with the domain message
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
