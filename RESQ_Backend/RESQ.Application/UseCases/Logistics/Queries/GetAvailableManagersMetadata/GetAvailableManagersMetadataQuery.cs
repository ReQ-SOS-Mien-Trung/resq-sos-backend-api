using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAvailableManagersMetadata;

/// <summary>
/// [Metadata] Danh sách Manager (RoleId=4) dùng cho dropdown gán manager. Loại trừ account bị ban.
/// Nếu truyền DepotId, loại trừ manager đang active trong kho đó.
/// </summary>
public record GetAvailableManagersMetadataQuery(int? DepotId = null) : IRequest<List<AvailableManagerDto>>;
