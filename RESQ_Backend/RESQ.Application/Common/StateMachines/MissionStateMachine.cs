using RESQ.Application.Exceptions;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Common.StateMachines;

/// <summary>
/// Enforces valid Mission status transitions per the state diagram:
/// Planned → OnGoing → Completed | Incompleted
/// </summary>
public static class MissionStateMachine
{
    private static readonly Dictionary<MissionStatus, HashSet<MissionStatus>> _allowed = new()
    {
        [MissionStatus.Planned]     = [MissionStatus.OnGoing],
        [MissionStatus.OnGoing]     = [MissionStatus.Completed, MissionStatus.Incompleted],
        [MissionStatus.Completed]   = [],
        [MissionStatus.Incompleted] = [],
    };

    public static void EnsureValidTransition(MissionStatus from, MissionStatus to)
    {
        if (!_allowed.TryGetValue(from, out var allowed) || !allowed.Contains(to))
            throw new BadRequestException(
                $"Không thể chuyển trạng thái mission từ '{from}' sang '{to}'. " +
                $"Các chuyển đổi hợp lệ từ '{from}': [{string.Join(", ", _allowed.GetValueOrDefault(from, []))}].");
    }
}
