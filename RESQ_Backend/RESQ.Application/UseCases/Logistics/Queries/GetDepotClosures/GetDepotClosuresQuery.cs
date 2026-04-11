using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;

/// <summary>
/// Lấy toàn bộ lịch sử phiên đóng kho của một kho theo depotId.
/// </summary>
public record GetDepotClosuresQuery(int DepotId, Guid? RequestingUserId = null) : IRequest<List<DepotClosureDto>>;
