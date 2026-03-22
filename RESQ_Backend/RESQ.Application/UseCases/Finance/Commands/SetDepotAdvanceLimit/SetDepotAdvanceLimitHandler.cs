using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

/// <summary>
/// [Admin] Cập nhật hạn mức tự ứng (max_advance_limit) cho quỹ kho.
/// Giá trị = 0 nghĩa là không cho phép kho tự ứng (balance không được âm).
/// </summary>
public class SetDepotAdvanceLimitHandler : IRequestHandler<SetDepotAdvanceLimitCommand, Unit>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SetDepotAdvanceLimitHandler(IDepotFundRepository depotFundRepo, IUnitOfWork unitOfWork)
    {
        _depotFundRepo = depotFundRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(SetDepotAdvanceLimitCommand request, CancellationToken cancellationToken)
    {
        if (request.MaxAdvanceLimit < 0)
            throw new BadRequestException("Hạn mức tự ứng không được là số âm.");

        var depotFund = await _depotFundRepo.GetOrCreateByDepotIdAsync(request.DepotId, cancellationToken);
        depotFund.SetMaxAdvanceLimit(request.MaxAdvanceLimit);
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);
        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
