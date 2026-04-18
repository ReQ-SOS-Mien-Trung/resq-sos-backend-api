using MediatR;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyManagedDepots;

/// <summary>
/// Lấy danh sách kho mà manager hiện tại đang quản lý.
/// Dùng để frontend hiển thị dropdown chọn kho trước khi thực hiện thao tác.
/// </summary>
public record GetMyManagedDepotsQuery(Guid UserId) : IRequest<List<ManagedDepotDto>>;
