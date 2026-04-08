using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CompleteAssemblyPointMaintenance;

/// <summary>
/// Admin hoàn tất bảo trì, đưa điểm tập kết về hoạt động: UnderMaintenance → Active.
/// </summary>
public record CompleteAssemblyPointMaintenanceCommand(int Id) : IRequest<CompleteAssemblyPointMaintenanceResponse>;
