using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.StateMachines;

/// <summary>
/// Enforces valid state transitions for depot supply requests.
///
/// Luồng chuẩn (Source side):
///   Pending → Accepted  (Accept — kho nguồn chấp nhận)
///   Pending → Rejected  (Reject — kho nguồn từ chối)
///   Accepted → Preparing (Prepare — kho nguồn bắt đầu đóng gói)
///   Preparing → Shipping (Ship   — kho nguồn xuất kho, bắt đầu vận chuyển)
///   Shipping → Completed (Complete — kho nguồn xác nhận đã giao hàng)
///
/// Luồng chuẩn (Requesting side — driven bởi hành động của source):
///   WaitingForApproval → Approved  (khi source Accept)
///   WaitingForApproval → Rejected  (khi source Reject)
///   Approved → InTransit           (khi source Ship)
///   InTransit → Received           (khi requesting Confirm — CHỈ sau khi source đã Completed)
///
/// Trạng thái kết thúc: Completed (source) + Received (requesting) | Rejected (cả hai)
/// </summary>
public static class SupplyRequestStateMachine
{
    /// <summary>
    /// Kho nguồn chấp nhận yêu cầu.
    /// Source: Pending → Accepted | Requesting: WaitingForApproval → Approved
    /// </summary>
    public static void EnsureCanAccept(string sourceStatus, string requestingStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Pending))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể chấp nhận khi yêu cầu đang ở trạng thái '{nameof(SourceDepotStatus.Pending)}' (hiện tại: {sourceStatus}).");

        if (requestingStatus != nameof(RequestingDepotStatus.WaitingForApproval))
            throw new InvalidSupplyRequestStateException(
                $"Trạng thái kho yêu cầu không hợp lệ: kỳ vọng '{nameof(RequestingDepotStatus.WaitingForApproval)}', hiện tại: {requestingStatus}.");
    }

    /// <summary>
    /// Kho nguồn từ chối yêu cầu.
    /// Source: Pending → Rejected | Requesting: WaitingForApproval → Rejected
    /// </summary>
    public static void EnsureCanReject(string sourceStatus, string requestingStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Pending))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể từ chối khi yêu cầu đang ở trạng thái '{nameof(SourceDepotStatus.Pending)}' (hiện tại: {sourceStatus}).");

        if (requestingStatus != nameof(RequestingDepotStatus.WaitingForApproval))
            throw new InvalidSupplyRequestStateException(
                $"Trạng thái kho yêu cầu không hợp lệ: kỳ vọng '{nameof(RequestingDepotStatus.WaitingForApproval)}', hiện tại: {requestingStatus}.");
    }

    /// <summary>
    /// Kho nguồn bắt đầu đóng gói / picking.
    /// Source: Accepted → Preparing | Requesting: không đổi (Approved)
    /// </summary>
    public static void EnsureCanPrepare(string sourceStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Accepted))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể bắt đầu chuẩn bị khi yêu cầu đang ở trạng thái '{nameof(SourceDepotStatus.Accepted)}' (hiện tại: {sourceStatus}).");
    }

    /// <summary>
    /// Kho nguồn xuất kho và bắt đầu vận chuyển.
    /// Source: Preparing → Shipping | Requesting: Approved → InTransit
    /// </summary>
    public static void EnsureCanShip(string sourceStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Preparing))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể xuất kho khi yêu cầu đang ở trạng thái '{nameof(SourceDepotStatus.Preparing)}' (hiện tại: {sourceStatus}).");
    }

    /// <summary>
    /// Kho nguồn xác nhận đã hoàn tất giao hàng.
    /// Source: Shipping → Completed | Requesting: không đổi (InTransit) — chờ kho yêu cầu confirm
    /// </summary>
    public static void EnsureCanComplete(string sourceStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Shipping))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể xác nhận hoàn tất giao hàng khi yêu cầu đang ở trạng thái '{nameof(SourceDepotStatus.Shipping)}' (hiện tại: {sourceStatus}).");
    }

    /// <summary>
    /// Kho yêu cầu xác nhận đã nhận hàng — chỉ hợp lệ SAU KHI kho nguồn đã Completed.
    /// Source: không đổi (Completed) | Requesting: InTransit → Received
    /// </summary>
    public static void EnsureCanConfirmReceived(string sourceStatus, string requestingStatus)
    {
        if (sourceStatus != nameof(SourceDepotStatus.Completed))
            throw new InvalidSupplyRequestStateException(
                $"Kho nguồn chưa xác nhận hoàn tất giao hàng — kỳ vọng '{nameof(SourceDepotStatus.Completed)}', hiện tại: {sourceStatus}.");

        if (requestingStatus != nameof(RequestingDepotStatus.InTransit))
            throw new InvalidSupplyRequestStateException(
                $"Chỉ có thể xác nhận nhận hàng khi đang ở trạng thái '{nameof(RequestingDepotStatus.InTransit)}' (hiện tại: {requestingStatus}).");
    }
}
