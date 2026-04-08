using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.StartAssemblyPointMaintenance;

/// <summary>
/// Admin đưa điểm tập kết vào bảo trì: Active → UnderMaintenance hoặc Overloaded → UnderMaintenance.
/// </summary>
public record StartAssemblyPointMaintenanceCommand(int Id) : IRequest<StartAssemblyPointMaintenanceResponse>;
