using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAvailableManagersMetadata;

/// <summary>
/// [Metadata] Danh sách Manager (RoleId=4) chưa quản lý kho nào - dùng cho dropdown gán manager.
/// </summary>
public record GetAvailableManagersMetadataQuery : IRequest<List<AvailableManagerDto>>;
