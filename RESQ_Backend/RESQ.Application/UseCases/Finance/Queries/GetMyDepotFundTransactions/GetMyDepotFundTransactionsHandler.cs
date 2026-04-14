using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFundTransactions;

public class GetMyDepotFundTransactionsHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepo,
    IDepotFundRepository depotFundRepo)
    : IRequestHandler<GetMyDepotFundTransactionsQuery, PagedResult<DepotFundTransactionDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepo = depotInventoryRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotFundRepository _depotFundRepo = depotFundRepo;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<DepotFundTransactionDto>> Handle(
        GetMyDepotFundTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve depot ID từ tài khoản manager
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        // 2. Lấy giao dịch quỹ theo phân trang
        var pagedResult = await _depotFundRepo.GetPagedTransactionsByDepotIdAsync(
            depotId,
            request.PageNumber,
            request.PageSize,
            cancellationToken: cancellationToken);

        var dtos = pagedResult.Items.Select(t => new DepotFundTransactionDto
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
        }).ToList();

        return new PagedResult<DepotFundTransactionDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }
}
