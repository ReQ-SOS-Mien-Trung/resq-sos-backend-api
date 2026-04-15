using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CloseAssemblyPoint;

/// <summary>
/// Admin đóng vĩnh viễn điểm tập kết: Unavailable → Closed (hoặc Created → Closed).
/// Lý do đóng là bắt buộc. Toàn bộ rescuer sẽ được tự động unassign.
/// </summary>
public record CloseAssemblyPointCommand(int Id, Guid ChangedBy, string Reason) : IRequest<CloseAssemblyPointResponse>;
