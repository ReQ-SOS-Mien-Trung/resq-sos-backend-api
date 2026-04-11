using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotAdvanceTransactions;

public class GetMyDepotAdvanceTransactionsHandler(
    IDepotInventoryRepository depotInventoryRepo,
    IDepotFundRepository depotFundRepo)
    : IRequestHandler<GetMyDepotAdvanceTransactionsQuery, PagedResult<DepotFundTransactionDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;

    public async Task<PagedResult<DepotFundTransactionDto>> Handle(
        GetMyDepotAdvanceTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại chưa được phân công quản lý kho đang hoạt động.");

        var pagedResult = await _depotFundRepo.GetPagedTransactionsByDepotIdAsync(
            depotId,
            request.PageNumber,
            request.PageSize,
            [DepotFundTransactionType.PersonalAdvance, DepotFundTransactionType.AdvanceRepayment],
            cancellationToken);

        var contributorInputs = pagedResult.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.ContributorName) && !string.IsNullOrWhiteSpace(x.ContributorPhoneNumber))
            .Select(x => new ContributorDebtModel
            {
                ContributorName = x.ContributorName!,
                ContributorPhoneNumber = x.ContributorPhoneNumber!
            })
            .DistinctBy(x => $"{x.ContributorName}|{x.ContributorPhoneNumber}")
            .ToList();

        var contributorDebts = contributorInputs.Count == 0
            ? []
            : await _depotFundRepo.GetContributorDebtsByDepotAsync(depotId, contributorInputs, cancellationToken);

        var debtMap = contributorDebts.ToDictionary(
            x => $"{x.ContributorName}|{x.ContributorPhoneNumber}",
            x => x);

        var dtos = pagedResult.Items.Select(t =>
        {
            var dto = new DepotFundTransactionDto
            {
                Id = t.Id,
                DepotFundId = t.DepotFundId,
                TransactionType = t.TransactionType.ToString(),
                Amount = t.Amount,
                ReferenceType = t.ReferenceType,
                ReferenceId = t.ReferenceId,
                Note = t.Note,
                CreatedBy = t.CreatedBy,
                CreatedAt = t.CreatedAt.ToVietnamTime(),
                ContributorName = t.ContributorName,
                ContributorPhoneNumber = t.ContributorPhoneNumber
            };

            if (!string.IsNullOrWhiteSpace(t.ContributorName)
                && !string.IsNullOrWhiteSpace(t.ContributorPhoneNumber)
                && debtMap.TryGetValue($"{t.ContributorName}|{t.ContributorPhoneNumber}", out var debt))
            {
                var outstanding = Math.Max(0m, debt.TotalAdvancedAmount - debt.TotalRepaidAmount);
                var repaidPercentage = debt.TotalAdvancedAmount <= 0m
                    ? 100m
                    : Math.Min(100m, Math.Round(debt.TotalRepaidAmount / debt.TotalAdvancedAmount * 100m, 2, MidpointRounding.AwayFromZero));

                dto.ContributorTotalAdvancedAmount = debt.TotalAdvancedAmount;
                dto.ContributorTotalRepaidAmount = debt.TotalRepaidAmount;
                dto.ContributorOutstandingAmount = outstanding;
                dto.ContributorRepaidPercentage = repaidPercentage;
            }

            return dto;
        }).ToList();

        return new PagedResult<DepotFundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
