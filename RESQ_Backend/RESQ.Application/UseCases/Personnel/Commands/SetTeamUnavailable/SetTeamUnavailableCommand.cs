using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamUnavailable;

/// <summary>
/// Đánh dấu đội không sẵn sàng nhận nhiệm vụ (Available → Unavailable).
/// Caller phải là đội trưởng của đội hoặc coordinator.
/// </summary>
public record SetTeamUnavailableCommand(int TeamId, Guid CallerUserId, int CallerRoleId) : IRequest;
