using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CloseAssemblyPoint;

/// <summary>
/// Admin đóng vĩnh viễn điểm tập kết: Active → Closed.
/// Yêu cầu không còn rescuer hoặc đội cứu hộ nào thuộc điểm tập kết này.
/// </summary>
public record CloseAssemblyPointCommand(int Id) : IRequest<CloseAssemblyPointResponse>;
