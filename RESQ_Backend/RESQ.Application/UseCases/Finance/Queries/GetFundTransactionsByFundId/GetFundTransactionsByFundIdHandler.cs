using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundTransactionsByFundId;

/// <summary>
/// Admin xem bất kỳ quỹ kho nào.
/// Quản kho chỉ xem được quỹ thuộc kho mình đang quản lý.
/// </summary>
public class GetFundTransactionsByFundIdHandler
    : IRequestHandler<GetFundTransactionsByFundIdQuery, PagedResult<DepotFundTransactionDto>>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IUserPermissionResolver _permissionResolver;

    public GetFundTransactionsByFundIdHandler(
        IDepotFundRepository depotFundRepo,
        IDepotInventoryRepository depotInventoryRepo,
        IUserPermissionResolver permissionResolver)
    {
        _depotFundRepo = depotFundRepo;
        _depotInventoryRepo = depotInventoryRepo;
        _permissionResolver = permissionResolver;
    }

    public async Task<PagedResult<DepotFundTransactionDto>> Handle(
        GetFundTransactionsByFundIdQuery request,
        CancellationToken cancellationToken)
    {
        var fund = await _depotFundRepo.GetByIdAsync(request.FundId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quỹ kho #{request.FundId}.");

        var permissions = await _permissionResolver.GetEffectivePermissionCodesAsync(request.RequestedBy, cancellationToken);
        var isAdmin = permissions.Contains(PermissionConstants.InventoryGlobalManage, StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var managedDepotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.RequestedBy, cancellationToken);
            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != fund.DepotId)
            {
                throw new ForbiddenException("Quỹ này không thuộc kho bạn đang quản lý.");
            }
        }

        var pagedResult = await _depotFundRepo.GetPagedTransactionsByFundIdAsync(
            request.FundId,
            request.PageNumber,
            request.PageSize,
            cancellationToken: cancellationToken);

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
            : await _depotFundRepo.GetContributorDebtsByDepotAsync(fund.DepotId, contributorInputs, cancellationToken);

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

        return new PagedResult<DepotFundTransactionDto>(dtos, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }
}
