using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

/// <summary>
/// DepotManager c?a chính depot dó m?i du?c báo cáo v?t ph?m dã mua.
/// Admin có th? thêm b?t k? lúc nào.
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
            ?? throw new NotFoundException($"Không tìm th?y gi?i ngân #{request.DisbursementId}.");

        if (!request.CanManageAnyDisbursement)
        {
            var depot = await _depotRepo.GetByIdAsync(disbursement.DepotId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm th?y depot #{disbursement.DepotId}.");

            if (depot.CurrentManagerId != request.CallerId)
                throw new ForbiddenException("B?n không ph?i là qu?n lý hi?n t?i c?a depot này.");
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
