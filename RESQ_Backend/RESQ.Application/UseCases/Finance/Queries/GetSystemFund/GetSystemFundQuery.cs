using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFund;

/// <summary>
/// [Admin] Xem thông tin quỹ hệ thống (số dư, tên, lần cập nhật cuối).
/// </summary>
public record GetSystemFundQuery() : IRequest<SystemFundDto>;
