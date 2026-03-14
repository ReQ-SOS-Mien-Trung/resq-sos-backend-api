using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public class GetRescuersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetRescuersQuery, PagedResult<GetUsersItemResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<PagedResult<GetUsersItemResponse>> Handle(GetRescuersQuery request, CancellationToken cancellationToken)
    {
        var paged = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            roleId: 3,
            isBanned: request.IsBanned,
            search: request.Search,
            isEligible: request.IsEligible,
            cancellationToken: cancellationToken);

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
