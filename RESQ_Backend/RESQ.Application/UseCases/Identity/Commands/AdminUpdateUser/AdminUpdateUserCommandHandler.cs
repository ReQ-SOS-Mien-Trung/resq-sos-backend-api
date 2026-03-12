using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AdminUpdateUser;

public class AdminUpdateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<AdminUpdateUserCommandHandler> logger
) : IRequestHandler<AdminUpdateUserCommand, AdminUpdateUserResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AdminUpdateUserCommandHandler> _logger = logger;

    public async Task<AdminUpdateUserResponse> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.UserId}");

        // Check unique constraints before updating
        if (!string.IsNullOrEmpty(request.Phone) && request.Phone != user.Phone)
        {
            var existing = await _userRepository.GetByPhoneAsync(request.Phone, cancellationToken);
            if (existing is not null)
                throw new ConflictException("Số điện thoại đã được sử dụng bởi tài khoản khác");
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existing is not null)
                throw new ConflictException("Email đã được sử dụng bởi tài khoản khác");
        }

        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            var existing = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
            if (existing is not null)
                throw new ConflictException("Username đã được sử dụng bởi tài khoản khác");
        }

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.Username = request.Username ?? user.Username;
        user.Phone = request.Phone ?? user.Phone;
        user.Email = request.Email ?? user.Email;
        user.RescuerType = request.RescuerType ?? user.RescuerType;
        if (request.RoleId.HasValue)
            user.RoleId = request.RoleId.Value;
        user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
        user.Address = request.Address ?? user.Address;
        user.Ward = request.Ward ?? user.Ward;
        user.Province = request.Province ?? user.Province;
        if (request.Latitude.HasValue) user.Latitude = request.Latitude;
        if (request.Longitude.HasValue) user.Longitude = request.Longitude;
        if (request.IsEmailVerified.HasValue) user.IsEmailVerified = request.IsEmailVerified.Value;
        if (request.IsOnboarded.HasValue) user.IsOnboarded = request.IsOnboarded.Value;
        if (request.IsEligibleRescuer.HasValue) user.IsEligibleRescuer = request.IsEligibleRescuer.Value;
        if (request.ApprovedBy.HasValue) user.ApprovedBy = request.ApprovedBy;
        if (request.ApprovedAt.HasValue) user.ApprovedAt = request.ApprovedAt;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Admin updated user UserId={UserId}", user.Id);

        return new AdminUpdateUserResponse
        {
            Id = user.Id,
            RoleId = user.RoleId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Phone = user.Phone,
            Email = user.Email,
            RescuerType = user.RescuerType.ToString(),
            AvatarUrl = user.AvatarUrl,
            Address = user.Address,
            Ward = user.Ward,
            Province = user.Province,
            Latitude = user.Latitude,
            Longitude = user.Longitude,
            IsEmailVerified = user.IsEmailVerified,
            IsOnboarded = user.IsOnboarded,
            IsEligibleRescuer = user.IsEligibleRescuer,
            IsBanned = user.IsBanned,
            BannedBy = user.BannedBy,
            BannedAt = user.BannedAt,
            BanReason = user.BanReason,
            ApprovedBy = user.ApprovedBy,
            ApprovedAt = user.ApprovedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
