using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

/// <summary>
/// Admin đưa điểm tập kết vào bảo trì: Active → UnderMaintenance hoặc Overloaded → UnderMaintenance.
/// </summary>
public record SetAssemblyPointUnavailableCommand(int Id) : IRequest<SetAssemblyPointUnavailableResponse>;

