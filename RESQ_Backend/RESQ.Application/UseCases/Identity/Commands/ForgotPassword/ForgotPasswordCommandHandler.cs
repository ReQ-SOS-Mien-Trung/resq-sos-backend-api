using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Commands.ForgotPassword
{
    public class ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<ForgotPasswordCommandHandler> logger
    ) : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<ForgotPasswordCommandHandler> _logger = logger;

        public async Task<ForgotPasswordResponse> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling ForgotPasswordCommand for Email={email}", request.Email);

            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

            // Always return success to avoid email enumeration
            if (user is null)
            {
                _logger.LogWarning("Forgot password: no account found for Email={email}", request.Email);
                return new ForgotPasswordResponse
                {
                    Success = true,
                    Message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi đi."
                };
            }

            // Generate reset token
            var resetToken = GenerateResetToken();
            var tokenExpiry = DateTime.UtcNow.AddHours(1);

            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = tokenExpiry;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new BadRequestException("Không thể tạo mã đặt lại mật khẩu. Vui lòng thử lại.");
            }

            // Send password reset email
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email!, resetToken, cancellationToken);
                _logger.LogInformation("Password reset email sent to {email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {email}", user.Email);
                throw new NetworkException("Không thể gửi email đặt lại mật khẩu. Vui lòng thử lại sau.");
            }

            return new ForgotPasswordResponse
            {
                Success = true,
                Message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi đi."
            };
        }

        private static string GenerateResetToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "")
                + Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
        }
    }
}
