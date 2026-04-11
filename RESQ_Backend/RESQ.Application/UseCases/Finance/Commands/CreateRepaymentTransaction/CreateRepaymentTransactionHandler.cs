using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

/// <summary>
/// [Admin] Xử lý logic hoàn trả tiền ứng trước cho cá nhân từ quỹ kho.
/// </summary>
public class CreateRepaymentTransactionHandler : IRequestHandler<CreateRepaymentTransactionCommand, Unit>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRepaymentTransactionHandler(IDepotFundRepository depotFundRepo, IUnitOfWork unitOfWork)
    {
        _depotFundRepo = depotFundRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateRepaymentTransactionCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new BadRequestException("Số tiền hoàn trả phải lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            throw new BadRequestException("Phải cung cấp tên người nhận hoàn trả.");

        var depotFund = await _depotFundRepo.GetByIdAsync(request.DepotFundId, cancellationToken);
        if (depotFund == null)
            throw new NotFoundException($"Không tìm thấy quỹ kho có ID {request.DepotFundId}.");

        // Execute domain logic (thử trả tiền, throw nếu ko đủ quỹ hoặc vượt số tiền đã ứng)
        depotFund.Repay(request.Amount);
        
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // Record the transaction
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.AdvanceRepayment,
            Amount = request.Amount,
            ReferenceType = null,
            ReferenceId = null,
            Note = $"Hoàn trả {request.Amount:N0} VNĐ tiền quỹ kho đã ứng trước cho {request.ContributorName}.",
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ContributorName = request.ContributorName,
            ContributorId = request.ContributorId
        }, cancellationToken);

        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
