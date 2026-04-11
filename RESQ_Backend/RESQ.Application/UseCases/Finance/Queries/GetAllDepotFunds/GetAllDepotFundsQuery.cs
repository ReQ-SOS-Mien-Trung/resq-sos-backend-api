using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

/// <summary>
/// [Admin] Xem quỹ tất cả kho.
/// </summary>
public record GetAllDepotFundsQuery() : IRequest<List<DepotFundsResponseDto>>;
