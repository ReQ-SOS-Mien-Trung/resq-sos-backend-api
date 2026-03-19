using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

/// <summary>
/// Team xác nhận đã lấy hàng tại kho cho một activity.
/// Hệ thống sẽ trừ ReservedQuantity và Quantity trong kho tương ứng.
/// </summary>
public record ConfirmMissionSupplyPickupCommand(
    int ActivityId,
    int MissionId,
    Guid UserId
) : IRequest<ConfirmMissionSupplyPickupResponse>;
