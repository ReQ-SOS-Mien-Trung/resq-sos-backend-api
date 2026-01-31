using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Users;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Users.Commands.ResendVerificationEmail
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
                    Message = "If the email exists in our system, a verification email will be sent."
                };
            }

            // Check if already verified
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for Email={email}", user.Email);
                throw new BadRequestException("Email is already verified");
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
                throw new BadRequestException("Failed to generate verification token. Please try again.");
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
                throw new NetworkException("Failed to send verification email. Please try again later.");
            }

            return new ResendVerificationEmailResponse
            {
                Success = true,
                Message = "Verification email sent successfully. Please check your inbox."
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
