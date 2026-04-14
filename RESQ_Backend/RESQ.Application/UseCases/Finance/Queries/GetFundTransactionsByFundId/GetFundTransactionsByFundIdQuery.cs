using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundTransactionsByFundId;

/// <summary>
/// Lấy lịch sử giao dịch của một quỹ kho cụ thể (theo fund ID, không phải depot ID).
/// Admin có thể xem bất kỳ quỹ nào; Manager chỉ xem quỹ thuộc kho mình quản lý.
/// </summary>
public record GetFundTransactionsByFundIdQuery(
    int FundId,
    int PageNumber,
    int PageSize,
    Guid RequestedBy, int? DepotId = null) : IRequest<PagedResult<DepotFundTransactionDto>>;
