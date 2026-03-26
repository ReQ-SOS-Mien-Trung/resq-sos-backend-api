using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;

namespace RESQ.Application.UseCases.Identity.Queries.GetUsersForPermission;

public class GetUsersForPermissionQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUsersForPermissionQuery, PagedResult<GetUsersItemResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<PagedResult<GetUsersItemResponse>> Handle(
        GetUsersForPermissionQuery request, CancellationToken cancellationToken)
    {
        var paged = await _userRepository.GetPagedForPermissionAsync(
            request.PageNumber,
            request.PageSize,
            request.RoleId,
            request.Search,
            cancellationToken);

        var items = paged.Items.Select(u => new GetUsersItemResponse
        {
            Id = u.Id,
            RoleId = u.RoleId,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Username = u.Username,
            Phone = u.Phone,
            Email = u.Email,
            RescuerType = u.RescuerType.ToString(),
            AvatarUrl = u.AvatarUrl,
            IsEmailVerified = u.IsEmailVerified,
            IsEligibleRescuer = u.IsEligibleRescuer,
            RescuerStep = u.RescuerStep,
            IsBanned = u.IsBanned,
            BannedBy = u.BannedBy,
            BannedAt = u.BannedAt,
            BanReason = u.BanReason,
            Address = u.Address,
            Ward = u.Ward,
            Province = u.Province,
            Latitude = u.Latitude,
            Longitude = u.Longitude,
            ApprovedBy = u.ApprovedBy,
            ApprovedAt = u.ApprovedAt,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return new PagedResult<GetUsersItemResponse>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
