using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Common;

public static class SosRequestVictimMutationGuard
{
    public static void EnsureCanUpdate(SosRequestModel sosRequest)
        => EnsureCanMutate(sosRequest, "cập nhật");

    public static void EnsureCanCancel(SosRequestModel sosRequest)
        => EnsureCanMutate(sosRequest, "hủy");

    private static void EnsureCanMutate(SosRequestModel sosRequest, string action)
    {
        if (sosRequest.ClusterId.HasValue)
        {
            throw new BadRequestException(
                $"Không thể {action} SOS request #{sosRequest.Id} vì coordinator đã gom yêu cầu này vào cụm #{sosRequest.ClusterId.Value} để xử lý.");
        }

        if (sosRequest.Status != SosRequestStatus.Pending)
        {
            throw new BadRequestException(
                $"Không thể {action} SOS request #{sosRequest.Id} khi trạng thái hiện tại là {sosRequest.Status}. Chỉ có thể {action} khi request còn ở trạng thái Pending.");
        }
    }
}
