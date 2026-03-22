using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

/// <summary>
/// Admin gán hoặc thay đổi điểm tập kết cho rescuer.
/// AssemblyPointId = null → gỡ rescuer khỏi điểm tập kết.
/// </summary>
public record AssignRescuerToAssemblyPointCommand(
    Guid RescuerUserId,
    int? AssemblyPointId) : IRequest;
