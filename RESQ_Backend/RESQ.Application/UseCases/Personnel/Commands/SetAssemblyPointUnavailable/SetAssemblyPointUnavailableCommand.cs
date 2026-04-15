using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

/// <summary>
/// Admin đánh dấu điểm tập kết không khả dụng: Available → Unavailable.
/// </summary>
public record SetAssemblyPointUnavailableCommand(int Id, Guid ChangedBy, string? Reason = null) : IRequest<SetAssemblyPointUnavailableResponse>;

