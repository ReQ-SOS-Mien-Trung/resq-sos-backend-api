using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;

/// <summary>
/// [Admin] Xử lý logic ứng trước tiền cá nhân cho kho.
/// </summary>
public class CreateAdvanceTransactionHandler : IRequestHandler<CreateAdvanceTransactionCommand, Unit>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdvanceTransactionHandler(IDepotFundRepository depotFundRepo, IUnitOfWork unitOfWork)
    {
        _depotFundRepo = depotFundRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateAdvanceTransactionCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new BadRequestException("Số tiền ứng trước phải lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            throw new BadRequestException("Phải cung cấp tên người ứng trước.");

        var depotFund = await _depotFundRepo.GetByIdAsync(request.DepotFundId, cancellationToken);
        if (depotFund == null)
            throw new NotFoundException($"Không tìm thấy quỹ kho có ID {request.DepotFundId}.");

        // Execute domain logic
        depotFund.Advance(request.Amount);
        
        await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);

        // Record the transaction
        await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
        {
            DepotFundId = depotFund.Id,
            TransactionType = DepotFundTransactionType.PersonalAdvance,
            Amount = request.Amount,
            ReferenceType = null,
            ReferenceId = null,
            Note = $"Cá nhân {request.ContributorName} ứng trước {request.Amount:N0} VNĐ cho kho.",
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ContributorName = request.ContributorName,
            ContributorId = request.ContributorId
        }, cancellationToken);

        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
