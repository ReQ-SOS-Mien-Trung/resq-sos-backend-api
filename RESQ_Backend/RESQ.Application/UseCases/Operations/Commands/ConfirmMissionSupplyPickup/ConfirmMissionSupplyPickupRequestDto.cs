using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupRequestDto
{
    /// <summary>
    /// Danh sách thông tin sử dụng buffer cho từng item. Chỉ truyền những item có dùng buffer.
    /// Nếu không truyền hoặc để trống, hệ thống ghi nhận không có buffer nào được sử dụng.
    /// </summary>
    public List<MissionPickupBufferUsageDto>? BufferUsages { get; set; }
}
