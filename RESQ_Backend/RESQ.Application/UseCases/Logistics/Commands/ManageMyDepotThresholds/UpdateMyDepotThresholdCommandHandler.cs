using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class UpdateMyDepotThresholdCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IStockThresholdConfigRepository stockThresholdConfigRepository,
    IStockThresholdResolver stockThresholdResolver)
    : IRequestHandler<UpdateMyDepotThresholdCommand, StockThresholdCommandResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;
    private readonly IStockThresholdResolver _stockThresholdResolver = stockThresholdResolver;

    public async Task<StockThresholdCommandResponse> Handle(UpdateMyDepotThresholdCommand request, CancellationToken cancellationToken)
    {
        int depotId;

        if (request.CanManageGlobalThresholds)
        {
            // Caller có quyền toàn cục chỉ được cấu hình Global — depotId bị bỏ qua bởi UpsertAsync khi scope=Global
            depotId = 0;
        }
        else
        {
            // Caller quản lý kho: tìm kho đang quản lý
            depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");
        }

        if (request.CategoryId.HasValue)
        {
            var exists = await _stockThresholdConfigRepository.CategoryExistsAsync(request.CategoryId.Value, cancellationToken);
            if (!exists)
                throw new NotFoundException("Không tìm thấy categoryId.");
        }

        if (request.ItemModelId.HasValue)
        {
            var exists = await _stockThresholdConfigRepository.ItemModelExistsAsync(request.ItemModelId.Value, cancellationToken);
            if (!exists)
                throw new NotFoundException("Không tìm thấy itemModelId.");
        }

        var saved = await _stockThresholdConfigRepository.UpsertAsync(
            request.ScopeType,
            depotId,
            request.CategoryId,
            request.ItemModelId,
            dangerRatio: null,
            warningRatio: null,
            minimumThreshold: request.MinimumThreshold,
            request.UserId,
            request.RowVersion,
            request.Reason,
            cancellationToken);

        // Invalidate cache tương ứng theo scope
        if (request.ScopeType == StockThresholdScopeType.Global)
            await _stockThresholdResolver.InvalidateGlobalAsync();
        else
            await _stockThresholdResolver.InvalidateDepotScopeAsync(depotId);

        return new StockThresholdCommandResponse
        {
            ScopeType = saved.ScopeType.ToString(),
            DepotId = saved.DepotId,
            CategoryId = saved.CategoryId,
            ItemModelId = saved.ItemModelId,
            MinimumThreshold = saved.MinimumThreshold,
            RowVersion = saved.RowVersion,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật ngưỡng tồn kho thành công."
        };
    }
}
