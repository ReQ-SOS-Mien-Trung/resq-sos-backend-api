using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class ResetMyDepotThresholdCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IStockThresholdConfigRepository stockThresholdConfigRepository,
    IStockThresholdResolver stockThresholdResolver)
    : IRequestHandler<ResetMyDepotThresholdCommand, StockThresholdCommandResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IStockThresholdConfigRepository _stockThresholdConfigRepository = stockThresholdConfigRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IStockThresholdResolver _stockThresholdResolver = stockThresholdResolver;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<StockThresholdCommandResponse> Handle(ResetMyDepotThresholdCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var reset = await _stockThresholdConfigRepository.ResetAsync(
            request.ScopeType,
            depotId,
            request.CategoryId,
            request.ItemModelId,
            request.UserId,
            request.RowVersion,
            request.Reason,
            cancellationToken);

        await _stockThresholdResolver.InvalidateDepotScopeAsync(depotId);

        if (reset == null)
        {
            return new StockThresholdCommandResponse
            {
                ScopeType = request.ScopeType.ToString(),
                DepotId = depotId,
                CategoryId = request.CategoryId,
                ItemModelId = request.ItemModelId,
                Message = "Không có cấu hình active để reset."
            };
        }

        return new StockThresholdCommandResponse
        {
            ScopeType = request.ScopeType.ToString(),
            DepotId = depotId,
            CategoryId = request.CategoryId,
            ItemModelId = request.ItemModelId,
            Message = "Reset cấu hình ngưỡng tồn kho thành công."
        };
    }
}
