using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Users;

namespace RESQ.Application.UseCases.Users.Commands.Logout
{
    public class LogoutCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<LogoutCommandHandler> logger
    ) : IRequestHandler<LogoutCommand, LogoutResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<LogoutCommandHandler> _logger = logger;

        public async Task<LogoutResponse> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling LogoutCommand for UserId={userId}", request.UserId);

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("User", request.UserId);
            }

            // Clear refresh token to invalidate it
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("User logged out successfully: UserId={userId}", request.UserId);

            return new LogoutResponse
            {
                Success = true,
                Message = "Đăng xuất thành công"
            };
        }
    }
}
