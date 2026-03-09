using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UnbanUser;

public class UnbanUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<UnbanUserCommandHandler> logger
) : IRequestHandler<UnbanUserCommand, UnbanUserResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UnbanUserCommandHandler> _logger = logger;

    public async Task<UnbanUserResponse> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.TargetUserId}");

        if (!user.IsBanned)
            throw new ConflictException("User này chưa bị ban");

        user.IsBanned = false;
        user.BannedBy = null;
        user.BannedAt = null;
        user.BanReason = null;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("User {TargetUserId} was unbanned", request.TargetUserId);

        return new UnbanUserResponse { UserId = user.Id, IsBanned = false };
    }
}
