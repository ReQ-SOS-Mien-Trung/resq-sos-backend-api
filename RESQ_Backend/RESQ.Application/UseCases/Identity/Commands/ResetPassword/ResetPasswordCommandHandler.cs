using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.ResetPassword
{
    public class ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<ResetPasswordCommandHandler> logger
    ) : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<ResetPasswordCommandHandler> _logger = logger;

        public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling ResetPasswordCommand");

            if(request.NewPassword != request.ConfirmPassword)
            {
                _logger.LogWarning("Reset password failed: New password and confirm password do not match");
                throw new BadRequestException("Mật khẩu mới và xác nhận mật khẩu không khớp");
            }

            // Find user by reset token
            var user = await _userRepository.GetByPasswordResetTokenAsync(request.Token, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Reset password failed: Invalid token");
                throw new BadRequestException("Token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn");
            }

            // Check if token is expired
            if (user.PasswordResetTokenExpiry.HasValue && user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Reset password failed: Token expired for UserId={userId}", user.Id);
                throw new BadRequestException("Token đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới.");
            }

            // Hash new password
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            // Clear reset token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new BadRequestException("Không thể đặt lại mật khẩu. Vui lòng thử lại.");
            }

            _logger.LogInformation("Password reset successfully for UserId={userId}", user.Id);

            return new ResetPasswordResponse
            {
                Success = true,
                Message = "Mật khẩu đã được đặt lại thành công."
            };
        }
    }
}
