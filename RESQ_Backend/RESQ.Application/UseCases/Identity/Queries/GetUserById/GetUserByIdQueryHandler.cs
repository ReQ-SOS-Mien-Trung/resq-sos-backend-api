using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetUserById;

public class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, GetUserByIdResponse>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<GetUserByIdResponse> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.UserId}");

        return new GetUserByIdResponse
        {
            Id = user.Id,
            RoleId = user.RoleId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Phone = user.Phone,
            Email = user.Email,
            RescuerType = user.RescuerType,
            AvatarUrl = user.AvatarUrl,
            IsEmailVerified = user.IsEmailVerified,
            IsOnboarded = user.IsOnboarded,
            IsEligibleRescuer = user.IsEligibleRescuer,
            IsBanned = user.IsBanned,
            BannedBy = user.BannedBy,
            BannedAt = user.BannedAt,
            BanReason = user.BanReason,
            Address = user.Address,
            Ward = user.Ward,
            Province = user.Province,
            Latitude = user.Latitude,
            Longitude = user.Longitude,
            ApprovedBy = user.ApprovedBy,
            ApprovedAt = user.ApprovedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
