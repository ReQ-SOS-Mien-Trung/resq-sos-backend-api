using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;

public class GetInventoryTransactionHistoryQueryHandler(IInventoryLogRepository inventoryLogRepository, IDepotInventoryRepository depotInventoryRepository) 
    : IRequestHandler<GetInventoryTransactionHistoryQuery, PagedResult<InventoryTransactionDto>>
{
    private readonly IInventoryLogRepository _inventoryLogRepository = inventoryLogRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<PagedResult<InventoryTransactionDto>> Handle(GetInventoryTransactionHistoryQuery request, CancellationToken cancellationToken)
    {
        // Get depot ID managed by this user
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");
        
        // Convert enums to strings for repository
        var actionTypeStrings = request.ActionTypes?.Select(x => x.ToString()).ToList();
        var sourceTypeStrings = request.SourceTypes?.Select(x => x.ToString()).ToList();
        
        return await _inventoryLogRepository.GetTransactionHistoryAsync(
            depotId,
            actionTypeStrings,
            sourceTypeStrings,
            request.FromDate,
            request.ToDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    private static string FormatQuantityChange(string actionType, int quantityChange)
    {
        return actionType.ToLower() switch
        {
            "import" or "transferin" or "return" => $"+ {quantityChange}",
            "export" or "transferout" => $"- {Math.Abs(quantityChange)}",
            "adjust" => quantityChange >= 0 ? $"+ {quantityChange}" : $"- {Math.Abs(quantityChange)}",
            _ => quantityChange.ToString()
        };
    }
}
