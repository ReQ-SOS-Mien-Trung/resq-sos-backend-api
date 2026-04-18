using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;

namespace RESQ.Application.UseCases.Identity.Queries.GetUsersForPermission;

/// <summary>
/// Lấy danh sách user để admin phân quyền.
/// Loại trừ: user bị ban, và những volunteer chưa được kích hoạt
/// (IsEligibleRescuer = false).
/// </summary>
public record GetUsersForPermissionQuery(
    int PageNumber,
    int PageSize,
    int? RoleId,
    string? Search
) : IRequest<PagedResult<GetUsersItemResponse>>;
