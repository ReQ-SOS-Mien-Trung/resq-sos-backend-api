using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class RestoreThresholdCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IStockThresholdConfigRepository stockThresholdConfigRepository,
    IStockThresholdResolver stockThresholdResolver)
    : IRequestHandler<RestoreThresholdCommand, StockThresholdCommandResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;
    private readonly IStockThresholdResolver _stockThresholdResolver = stockThresholdResolver;

    public async Task<StockThresholdCommandResponse> Handle(RestoreThresholdCommand request, CancellationToken cancellationToken)
    {
        int? managerDepotId = null;

        if (request.RoleId == 4)
        {
            // Manager: cần biết kho đang quản lý để validate quyền
            managerDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");
        }
        // Admin (role=1): managerDepotId = null → chỉ restore GLOBAL (validated in repo)

        var restored = await _stockThresholdConfigRepository.RestoreAsync(
            request.ConfigId,
            managerDepotId,
            request.UserId,
            request.Reason,
            cancellationToken);

        // Invalidate cache tương ứng với scope vừa khôi phục
        if (restored.ScopeType == StockThresholdScopeType.Global)
            await _stockThresholdResolver.InvalidateGlobalAsync();
        else
            await _stockThresholdResolver.InvalidateDepotScopeAsync(restored.DepotId!.Value);

        return new StockThresholdCommandResponse
        {
            ScopeType = restored.ScopeType.ToString(),
            DepotId = restored.DepotId,
            CategoryId = restored.CategoryId,
            ItemModelId = restored.ItemModelId,
            DangerPercent = restored.DangerRatio * 100m,
            WarningPercent = restored.WarningRatio * 100m,
            RowVersion = restored.RowVersion,
            UpdatedAt = restored.UpdatedAt,
            Message = "Khôi phục cấu hình ngưỡng tồn kho thành công."
        };
    }
}
