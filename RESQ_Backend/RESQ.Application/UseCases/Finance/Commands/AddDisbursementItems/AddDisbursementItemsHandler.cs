using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

/// <summary>
/// DepotManager của chính depot đó mới được báo cáo vật phẩm đã mua.
/// Admin có thể thêm bất kỳ lúc nào.
/// </summary>
public class AddDisbursementItemsHandler : IRequestHandler<AddDisbursementItemsCommand, Unit>
{
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IDepotRepository _depotRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AddDisbursementItemsHandler(
        ICampaignDisbursementRepository disbursementRepo,
        IDepotRepository depotRepo,
        IUnitOfWork unitOfWork)
    {
        _disbursementRepo = disbursementRepo;
        _depotRepo = depotRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(AddDisbursementItemsCommand request, CancellationToken cancellationToken)
    {
        var disbursement = await _disbursementRepo.GetByIdAsync(request.DisbursementId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy giải ngân #{request.DisbursementId}.");

        if (!request.CanManageAnyDisbursement)
        {
            var depot = await _depotRepo.GetByIdAsync(disbursement.DepotId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy depot #{disbursement.DepotId}.");

            if (depot.CurrentManagerId != request.CallerId)
                throw new ForbiddenException("Bạn không phải là quản lý hiện tại của depot này.");
        }

        var newItems = request.Items.Select(item => new DisbursementItemModel
        {
            CampaignDisbursementId = disbursement.Id,
            ItemName = item.ItemName,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            Note = item.Note,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _disbursementRepo.AddItemsAsync(disbursement.Id, newItems, cancellationToken);
        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
