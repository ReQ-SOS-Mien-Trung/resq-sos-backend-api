using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail
{
    public class ResendVerificationEmailCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<ResendVerificationEmailCommandHandler> logger
    ) : IRequestHandler<ResendVerificationEmailCommand, ResendVerificationEmailResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<ResendVerificationEmailCommandHandler> _logger = logger;

        public async Task<ResendVerificationEmailResponse> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling ResendVerificationEmailCommand for Email={email}", request.Email);

            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

            if (user is null)
            {
                // Don't reveal if email exists or not for security
                _logger.LogWarning("Resend verification failed: User not found for Email={email}", request.Email);
                return new ResendVerificationEmailResponse
                {
                    Success = true,
                    Message = "Nếu email tồn tại trong hệ thống, email xác minh sẽ được gửi đi."
                };
            }

            // Check if already verified
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for Email={email}", user.Email);
                throw new BadRequestException("Email đã được xác minh");
            }

            // Generate new verification token
            var verificationToken = GenerateVerificationToken();
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            user.EmailVerificationToken = verificationToken;
            user.EmailVerificationTokenExpiry = tokenExpiry;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new BadRequestException("Không thể tạo mã xác minh. Vui lòng thử lại.");
            }

            // Send verification email
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email!, verificationToken, cancellationToken);
                _logger.LogInformation("Verification email resent to {email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend verification email to {email}", user.Email);
                throw new NetworkException("Không thể gửi email xác minh. Vui lòng thử lại sau.");
            }

            return new ResendVerificationEmailResponse
            {
                Success = true,
                Message = "Email xác minh đã được gửi thành công. Vui lòng kiểm tra hộp thư đến."
            };
        }

        private static string GenerateVerificationToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");
        }
    }
}
