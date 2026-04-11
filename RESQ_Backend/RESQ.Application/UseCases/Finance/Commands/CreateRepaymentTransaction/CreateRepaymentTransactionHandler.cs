using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Common;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

public class CreateRepaymentTransactionHandler : IRequestHandler<CreateRepaymentTransactionCommand, Unit>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRepaymentTransactionHandler(
        IDepotFundRepository depotFundRepo,
        IDepotRepository depotRepo,
        IDepotInventoryRepository depotInventoryRepo,
        IUnitOfWork unitOfWork)
    {
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
        _depotInventoryRepo = depotInventoryRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateRepaymentTransactionCommand request, CancellationToken cancellationToken)
    {
        var contributorName = ContributorIdentityNormalizer.NormalizeName(request.ContributorName);
        var phoneNumber = ContributorIdentityNormalizer.NormalizePhoneNumber(request.PhoneNumber);

        if (request.Repayments.Count == 0)
        {
            throw new BadRequestException("Bắt buộc chọn ít nhất 1 quỹ để hoàn trả nợ cho người ứng.");
        }

        if (request.Repayments.Count > 200)
        {
            throw new BadRequestException("Mỗi lần chỉ được tối đa 200 quỹ hoàn trả.");
        }

        var managerDepotIds = await _depotInventoryRepo.GetActiveDepotIdsByManagerAsync(request.RequestedBy, cancellationToken);
        if (managerDepotIds.Count == 0)
        {
            throw new NotFoundException("Tài khoản hiện tại chưa được phân công quản lý kho đang hoạt động.");
        }

        var mergedRepayments = request.Repayments
            .GroupBy(x => x.DepotFundId)
            .Select(g => new RepaymentFundAllocation(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        if (mergedRepayments.Any(x => x.Amount <= 0))
        {
            throw new BadRequestException("Số tiền hoàn trả phải lớn hơn 0.");
        }

        var requestedFundIds = mergedRepayments.Select(x => x.DepotFundId).Distinct().ToList();
        var funds = await _depotFundRepo.GetByIdsAsync(requestedFundIds, cancellationToken);
        var fundById = funds.ToDictionary(x => x.Id);

        if (requestedFundIds.Any(x => !fundById.ContainsKey(x)))
        {
            throw new NotFoundException("Không tìm thấy một hoặc nhiều quỹ kho được chọn.");
        }

        var targetDepotId = funds.First().DepotId;
        if (funds.Any(x => x.DepotId != targetDepotId))
        {
            throw new BadRequestException("Các quỹ được chọn phải cùng thuộc một kho.");
        }

        if (!managerDepotIds.Contains(targetDepotId))
        {
            throw new ForbiddenException("Bạn chỉ được hoàn trả trên các quỹ thuộc kho bạn đang được phân công.");
        }

        var depot = await _depotRepo.GetByIdAsync(targetDepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho có id {targetDepotId}.");

        var contributorDebtInput = new ContributorDebtModel
        {
            ContributorName = contributorName,
            ContributorPhoneNumber = phoneNumber
        };

        var debtByFundRows = await _depotFundRepo.GetContributorDebtsByFundAsync(
            targetDepotId,
            requestedFundIds,
            [contributorDebtInput],
            cancellationToken);

        var outstandingByFund = debtByFundRows.ToDictionary(
            x => x.DepotFundId,
            x => x.TotalAdvancedAmount - x.TotalRepaidAmount);

        foreach (var repayment in mergedRepayments)
        {
            var outstanding = outstandingByFund.GetValueOrDefault(repayment.DepotFundId, 0m);
            if (repayment.Amount > outstanding)
            {
                throw new BadRequestException(
                    $"Vượt quá số nợ còn lại của người ứng {contributorName} ({phoneNumber}) tại quỹ {repayment.DepotFundId}. Còn nợ: {outstanding:N0}, yêu cầu trả: {repayment.Amount:N0}.");
            }
        }

        var contributorDebt = (await _depotFundRepo.GetContributorDebtsByDepotAsync(
            targetDepotId,
            [contributorDebtInput],
            cancellationToken)).FirstOrDefault()
            ?? new ContributorDebtModel
            {
                ContributorName = contributorName,
                ContributorPhoneNumber = phoneNumber,
                TotalAdvancedAmount = 0,
                TotalRepaidAmount = 0
            };

        var currentTotalAdvanced = contributorDebt.TotalAdvancedAmount;
        var currentTotalRepaid = contributorDebt.TotalRepaidAmount;
        var totalRepaymentAmount = mergedRepayments.Sum(x => x.Amount);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.RecordRepay(totalRepaymentAmount);

            foreach (var repayment in mergedRepayments.OrderBy(x => x.DepotFundId))
            {
                var fund = fundById[repayment.DepotFundId];
                fund.Repay(repayment.Amount);

                currentTotalRepaid += repayment.Amount;
                var outstanding = Math.Max(0m, currentTotalAdvanced - currentTotalRepaid);
                var repaidPercentage = currentTotalAdvanced <= 0m
                    ? 100m
                    : Math.Min(100m, Math.Round(currentTotalRepaid / currentTotalAdvanced * 100m, 2, MidpointRounding.AwayFromZero));

                await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                {
                    DepotFundId = fund.Id,
                    TransactionType = DepotFundTransactionType.AdvanceRepayment,
                    Amount = repayment.Amount,
                    ReferenceType = TransactionReferenceType.InternalRepayment.ToString(),
                    ReferenceId = depot.Id,
                    Note = $"Hoàn trả cho {contributorName} ({phoneNumber}): đã trả {repayment.Amount:N0} VND. Tiến độ {repaidPercentage:N2}% ({currentTotalRepaid:N0}/{currentTotalAdvanced:N0}), còn nợ {outstanding:N0} VND.",
                    CreatedBy = request.RequestedBy,
                    CreatedAt = DateTime.UtcNow,
                    ContributorName = contributorName,
                    ContributorPhoneNumber = phoneNumber
                }, cancellationToken);

                await _depotFundRepo.UpdateAsync(fund, cancellationToken);
            }

            await _depotRepo.UpdateAsync(depot, cancellationToken);
            await _unitOfWork.SaveAsync();
        });

        return Unit.Value;
    }
}
