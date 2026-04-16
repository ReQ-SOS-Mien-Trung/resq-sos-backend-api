using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

/// <summary>
/// [Admin] Xem quỹ tất cả kho (có phân trang).
/// </summary>
public record GetAllDepotFundsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null
) : IRequest<PagedResult<DepotFundsResponseDto>>;
