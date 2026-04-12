using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointAvailable;

/// <summary>
/// Admin hoàn tất bảo trì, đưa điểm tập kết về hoạt động: UnderMaintenance → Active.
/// </summary>
public record SetAssemblyPointAvailableCommand(int Id) : IRequest<SetAssemblyPointAvailableResponse>;

