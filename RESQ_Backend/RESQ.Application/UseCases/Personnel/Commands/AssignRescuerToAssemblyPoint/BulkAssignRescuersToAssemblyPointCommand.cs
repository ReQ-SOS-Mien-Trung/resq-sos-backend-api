using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

/// <summary>
/// Gán hoặc thay đổi điểm tập kết cho nhiều rescuer cùng lúc.
/// AssemblyPointId = null → gỡ tất cả khỏi điểm tập kết hiện tại.
/// </summary>
public record BulkAssignRescuersToAssemblyPointCommand(
    IReadOnlyList<Guid> UserIds,
    int? AssemblyPointId) : IRequest;
