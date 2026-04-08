using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.ActivateAssemblyPoint;

/// <summary>
/// Admin kích hoạt điểm tập kết: Created → Active.
/// </summary>
public record ActivateAssemblyPointCommand(int Id) : IRequest<ActivateAssemblyPointResponse>;
