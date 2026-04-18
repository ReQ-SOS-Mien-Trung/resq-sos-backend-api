using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// [Manager] Xem tất cả quỹ kho mà mình đang quản lý (có thể nhiều quỹ từ nhiều nguồn khác nhau).
/// </summary>
public record GetMyDepotFundQuery(Guid UserId, int? DepotId = null) : IRequest<MyDepotFundsResponseDto>;

