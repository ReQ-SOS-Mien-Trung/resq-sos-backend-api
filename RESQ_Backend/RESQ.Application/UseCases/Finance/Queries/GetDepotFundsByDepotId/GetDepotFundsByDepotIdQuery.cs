using MediatR;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotFundsByDepotId;

/// <summary>
/// [Admin] Lấy tất cả quỹ của một kho theo depot ID.
/// </summary>
public record GetDepotFundsByDepotIdQuery(int DepotId) : IRequest<DepotFundsResponseDto?>;
