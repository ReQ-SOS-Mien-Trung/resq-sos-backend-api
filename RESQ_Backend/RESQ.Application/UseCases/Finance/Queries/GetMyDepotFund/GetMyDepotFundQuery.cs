using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;

/// <summary>
/// [Manager] Xem quỹ kho mà mình đang quản lý.
/// </summary>
public record GetMyDepotFundQuery(Guid UserId) : IRequest<DepotFundDto>;
