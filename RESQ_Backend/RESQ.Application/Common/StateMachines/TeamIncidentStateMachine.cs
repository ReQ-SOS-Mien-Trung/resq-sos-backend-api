using RESQ.Application.Exceptions;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Common.StateMachines;

/// <summary>
/// Enforces valid TeamIncident status transitions per the state diagram:
/// Reported → InProgress → Resolved
/// </summary>
public static class TeamIncidentStateMachine
{
    private static readonly Dictionary<TeamIncidentStatus, HashSet<TeamIncidentStatus>> _allowed = new()
    {
        [TeamIncidentStatus.Reported]     = [TeamIncidentStatus.InProgress],
        [TeamIncidentStatus.InProgress]   = [TeamIncidentStatus.Resolved],
        [TeamIncidentStatus.Resolved]     = [],
    };

    public static void EnsureValidTransition(TeamIncidentStatus from, TeamIncidentStatus to)
    {
        if (!_allowed.TryGetValue(from, out var allowed) || !allowed.Contains(to))
            throw new BadRequestException(
                $"Không thể chuyển trạng thái sự cố từ '{from}' sang '{to}'. " +
                $"Các chuyển đổi hợp lệ từ '{from}': [{string.Join(", ", _allowed.GetValueOrDefault(from, []))}].");
    }
}
