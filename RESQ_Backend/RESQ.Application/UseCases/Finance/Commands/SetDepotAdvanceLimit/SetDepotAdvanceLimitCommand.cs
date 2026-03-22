using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

/// <summary>
/// [Admin] Cấu hình hạn mức tự ứng (balance âm) cho một kho.
/// </summary>
public record SetDepotAdvanceLimitCommand(
    int DepotId,
    decimal MaxAdvanceLimit
) : IRequest<Unit>;
