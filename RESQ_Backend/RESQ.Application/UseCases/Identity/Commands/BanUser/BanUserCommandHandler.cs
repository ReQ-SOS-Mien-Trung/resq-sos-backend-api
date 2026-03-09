using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.BanUser;

public class BanUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<BanUserCommandHandler> logger
) : IRequestHandler<BanUserCommand, BanUserResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<BanUserCommandHandler> _logger = logger;

    public async Task<BanUserResponse> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.TargetUserId}");

        if (user.IsBanned)
            throw new ConflictException("User này đã bị ban trước đó");

        user.IsBanned = true;
        user.BannedBy = request.AdminId;
        user.BannedAt = DateTime.UtcNow;
        user.BanReason = request.Reason;
        // Invalidate refresh token to force logout
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("User {TargetUserId} was banned by admin {AdminId}", request.TargetUserId, request.AdminId);

        return new BanUserResponse
        {
            UserId = user.Id,
            IsBanned = user.IsBanned,
            BannedBy = user.BannedBy,
            BannedAt = user.BannedAt,
            BanReason = user.BanReason
        };
    }
}
