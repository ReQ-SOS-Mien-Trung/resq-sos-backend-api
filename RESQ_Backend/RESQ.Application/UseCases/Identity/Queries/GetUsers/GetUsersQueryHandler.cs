using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetUsers;

public class GetUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUsersQuery, PagedResult<GetUsersItemResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<PagedResult<GetUsersItemResponse>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var paged = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.RoleId,
            request.IsBanned,
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
            RescuerType = u.RescuerType,
            AvatarUrl = u.AvatarUrl,
            IsEmailVerified = u.IsEmailVerified,
            IsOnboarded = u.IsOnboarded,
            IsEligibleRescuer = u.IsEligibleRescuer,
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
