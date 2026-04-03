using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;

/// <summary>
/// Admin huỷ yêu cầu đóng kho — kho khôi phục về trạng thái trước (Available/Full).
/// </summary>
public record CancelDepotClosureCommand(
    int DepotId,
    int ClosureId,
    Guid CancelledBy,
    string CancellationReason
) : IRequest<CancelDepotClosureResponse>;
