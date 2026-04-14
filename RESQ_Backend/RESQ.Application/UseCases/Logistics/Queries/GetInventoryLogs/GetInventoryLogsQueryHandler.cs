using MediatR;
using RESQ.Application.Extensions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;

public class GetInventoryLogsQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IInventoryLogRepository inventoryLogRepository,
    IDepotInventoryRepository depotInventoryRepository) 
    : IRequestHandler<GetInventoryLogsQuery, PagedResult<InventoryLogDto>>
{
    private readonly IInventoryLogRepository _inventoryLogRepository = inventoryLogRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<InventoryLogDto>> Handle(GetInventoryLogsQuery request, CancellationToken cancellationToken)
    {
        int? finalDepotId = request.DepotId;

        // Ensure managers can only see logs for their active depot
        if (request.IsManager)
        {
            var activeDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);
            if (!activeDepotId.HasValue)
            {
                throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");
            }
            finalDepotId = activeDepotId.Value; // Override any user-provided depot ID
        }

        var actionTypeStrings = request.ActionTypes?.Select(x => x.ToString()).ToList();
        var sourceTypeStrings = request.SourceTypes?.Select(x => x.ToString()).ToList();

        var pagedData = await _inventoryLogRepository.GetInventoryLogsPagedAsync(
            finalDepotId,
            request.ItemModelId,
            actionTypeStrings,
            sourceTypeStrings,
            request.FromDate,
            request.ToDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedData.Items.Select(log => new InventoryLogDto
        {
            Id = log.Id,
            DepotSupplyInventoryId = log.DepotSupplyInventoryId,
            SupplyInventoryLotId = log.SupplyInventoryLotId,
            ActionType = log.ActionType,
            QuantityChange = log.QuantityChange,
            FormattedQuantityChange = FormatQuantityChange(log.ActionType, log.QuantityChange),
            SourceType = log.SourceType,
            SourceId = log.SourceId,
            Note = log.Note,
            CreatedAt = log.CreatedAt.ToVietnamTime(),
            ReceivedDate = log.ReceivedDate.ToVietnamTime(),
            ExpiredDate = log.ExpiredDate.ToVietnamTime(),
            PerformedByName = log.PerformedByName,
            VatInvoiceId = log.VatInvoiceId,
            InvoiceSerial = log.InvoiceSerial,
            InvoiceNumber = log.InvoiceNumber,
            SupplierName = log.SupplierName,
            SupplierTaxCode = log.SupplierTaxCode,
            InvoiceDate = log.InvoiceDate,
            InvoiceTotalAmount = log.InvoiceTotalAmount,
            InvoiceFileUrl = log.InvoiceFileUrl
        }).ToList();

        return new PagedResult<InventoryLogDto>(dtos, pagedData.TotalCount, pagedData.PageNumber, pagedData.PageSize);
    }

    private static string FormatQuantityChange(string? actionType, int? quantityChange)
    {
        if (!quantityChange.HasValue || quantityChange.Value == 0)
            return "0";

        var isPositive = IsPositiveAction(actionType) && quantityChange.Value > 0;
        var isNegative = IsNegativeAction(actionType) && quantityChange.Value > 0;

        // If it's already negative, just show it as is
        if (quantityChange.Value < 0)
            return quantityChange.Value.ToString("N0");

        // For positive actions (Import, TransferIn, Return to stock)
        if (isPositive)
            return $"+ {quantityChange.Value:N0}";

        // For negative actions (Export, TransferOut) 
        if (isNegative)
            return $"- {quantityChange.Value:N0}";

        // For adjustments or unknown actions, use the raw value with appropriate sign
        return quantityChange.Value >= 0 
            ? $"+ {quantityChange.Value:N0}" 
            : quantityChange.Value.ToString("N0");
    }

    private static bool IsPositiveAction(string? actionType)
    {
        if (string.IsNullOrEmpty(actionType)) return false;
        
        return actionType.Equals(InventoryActionType.Import.ToString(), StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(InventoryActionType.TransferIn.ToString(), StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(InventoryActionType.Return.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNegativeAction(string? actionType)
    {
        if (string.IsNullOrEmpty(actionType)) return false;
        
        return actionType.Equals(InventoryActionType.Export.ToString(), StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(InventoryActionType.TransferOut.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
