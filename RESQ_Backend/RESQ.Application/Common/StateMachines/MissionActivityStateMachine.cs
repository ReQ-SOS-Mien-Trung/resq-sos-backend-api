using RESQ.Application.Exceptions;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Common.StateMachines;

/// <summary>
/// Enforces valid MissionActivity status transitions per the state diagram:
/// Planned → OnGoing → Succeed | Failed
/// Planned → Cancelled
/// OnGoing → Cancelled
/// </summary>
public static class MissionActivityStateMachine
{
    private static readonly Dictionary<MissionActivityStatus, HashSet<MissionActivityStatus>> _allowed = new()
    {
        [MissionActivityStatus.Planned]   = [MissionActivityStatus.OnGoing, MissionActivityStatus.Cancelled],
        [MissionActivityStatus.OnGoing]   = [MissionActivityStatus.Succeed, MissionActivityStatus.Failed, MissionActivityStatus.Cancelled],
        [MissionActivityStatus.Succeed]   = [],
        [MissionActivityStatus.Failed]    = [],
        [MissionActivityStatus.Cancelled] = [],
    };

    public static void EnsureValidTransition(MissionActivityStatus from, MissionActivityStatus to)
    {
        if (!_allowed.TryGetValue(from, out var allowed) || !allowed.Contains(to))
            throw new BadRequestException(
                $"Không thể chuyển trạng thái activity từ '{from}' sang '{to}'. " +
                $"Các chuyển đổi hợp lệ từ '{from}': [{string.Join(", ", _allowed.GetValueOrDefault(from, []))}].");
    }
}
