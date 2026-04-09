using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundTransactionsByFundId;

/// <summary>
/// Handler: Lấy lịch sử giao dịch của một quỹ kho theo fund ID.
/// - Admin: xem bất kỳ quỹ nào.
/// - Manager: chỉ xem quỹ thuộc kho mình đang quản lý.
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
        // 1. Load fund — xác nhận tồn tại
        var fund = await _depotFundRepo.GetByIdAsync(request.FundId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quỹ kho #{request.FundId}.");

        // 2. Ownership check: manager chỉ xem quỹ thuộc kho mình
        var permissions = await _permissionResolver.GetEffectivePermissionCodesAsync(request.RequestedBy, cancellationToken);
        var isAdmin = permissions.Contains(PermissionConstants.InventoryGlobalManage, StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var managedDepotId = await _depotInventoryRepo.GetActiveDepotIdByManagerAsync(request.RequestedBy, cancellationToken);
            if (managedDepotId != fund.DepotId)
                throw new ForbiddenException("Quỹ này không thuộc kho bạn đang quản lý.");
        }

        // 3. Lấy giao dịch theo fund ID
        var pagedResult = await _depotFundRepo.GetPagedTransactionsByFundIdAsync(
            request.FundId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedResult.Items.Select(t => new DepotFundTransactionDto
        {
            Id              = t.Id,
            DepotFundId     = t.DepotFundId,
            TransactionType = t.TransactionType.ToString(),
            Amount          = t.Amount,
            ReferenceType   = t.ReferenceType,
            ReferenceId     = t.ReferenceId,
            Note            = t.Note,
            CreatedBy       = t.CreatedBy,
            CreatedAt       = t.CreatedAt.ToVietnamTime()
        }).ToList();

        return new PagedResult<DepotFundTransactionDto>(dtos, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }
}
