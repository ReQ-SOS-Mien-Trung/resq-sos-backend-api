using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointAvailable;

/// <summary>
/// Admin phục hồi điểm tập kết về khả dụng: Unavailable → Available.
/// </summary>
public record SetAssemblyPointAvailableCommand(int Id, Guid ChangedBy, string? Reason = null) : IRequest<SetAssemblyPointAvailableResponse>;

