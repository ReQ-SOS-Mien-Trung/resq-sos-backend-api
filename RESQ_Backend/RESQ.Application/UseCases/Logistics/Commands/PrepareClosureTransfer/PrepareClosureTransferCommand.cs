using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận đang chuẩn bị hàng - chuyển transfer từ AwaitingPreparation → Preparing.
/// </summary>
/// <param name="DepotId">Kho nguồn (route param)</param>
/// <param name="TransferId">Bản ghi transfer</param>
/// <param name="UserId">Người thực hiện (manager của kho nguồn)</param>
/// <param name="Note">Ghi chú (tuỳ chọn)</param>
public record PrepareClosureTransferCommand(
    int TransferId,
    Guid UserId,
    string? Note) : IRequest<PrepareClosureTransferResponse>;
