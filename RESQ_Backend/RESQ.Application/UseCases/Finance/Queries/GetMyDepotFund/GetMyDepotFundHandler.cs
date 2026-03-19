using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// Handler: Lấy depot mà user đang quản lý → lấy quỹ kho tương ứng.
/// </summary>
public class GetMyDepotFundHandler : IRequestHandler<GetMyDepotFundQuery, DepotFundDto>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IDepotFundRepository _depotFundRepo;

    public GetMyDepotFundHandler(
        IDepotInventoryRepository depotInventoryRepo,
        IDepotFundRepository depotFundRepo)
    {
        _depotInventoryRepo = depotInventoryRepo;
        _depotFundRepo = depotFundRepo;
    }

    public async Task<DepotFundDto> Handle(GetMyDepotFundQuery request, CancellationToken cancellationToken)
    {
        // 1. Lấy depot mà user đang quản lý
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        // 2. Lấy quỹ kho (nếu chưa có record thì balance = 0)
        var fund = await _depotFundRepo.GetByDepotIdAsync(depotId, cancellationToken);

        return new DepotFundDto
        {
            DepotId = depotId,
            DepotName = fund?.DepotName,
            Balance = fund?.Balance ?? 0m,
            LastUpdatedAt = fund?.LastUpdatedAt
        };
    }
}
