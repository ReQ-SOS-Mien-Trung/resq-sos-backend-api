using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

/// <summary>
/// Team xác nhận thông tin sử dụng buffer dự trù khi lấy hàng tại kho cho một COLLECT_SUPPLIES activity.
/// Nếu có sử dụng buffer, phải truyền lý do. Việc trừ kho thực tế xảy ra khi activity được chuyển sang Succeed.
/// </summary>
public record ConfirmMissionSupplyPickupCommand(
    int ActivityId,
    int MissionId,
    Guid UserId,
    List<MissionPickupBufferUsageDto>? BufferUsages
) : IRequest<ConfirmMissionSupplyPickupResponse>;
