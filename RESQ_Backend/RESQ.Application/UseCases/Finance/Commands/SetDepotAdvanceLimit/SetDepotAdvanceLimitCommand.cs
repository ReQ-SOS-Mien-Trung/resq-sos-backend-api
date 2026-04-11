using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

/// <summary>
/// [Admin] Cấu hình hạn mức tổng tiền được phép ứng trước cho một kho.
/// </summary>
public record SetDepotAdvanceLimitCommand(
    int DepotId,
    decimal AdvanceLimit
) : IRequest<Unit>;
