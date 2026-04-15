using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.ActivateAssemblyPoint;

/// <summary>
/// Admin kích hoạt điểm tập kết: Created → Available.
/// </summary>
public record ActivateAssemblyPointCommand(int Id, Guid ChangedBy) : IRequest<ActivateAssemblyPointResponse>;
