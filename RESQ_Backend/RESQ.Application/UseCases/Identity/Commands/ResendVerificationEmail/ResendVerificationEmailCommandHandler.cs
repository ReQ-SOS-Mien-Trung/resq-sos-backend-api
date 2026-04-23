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
                    Message = "Nß║┐u email tß╗ôn tß║íi trong hß╗ç thß╗æng, email x├íc minh sß║╜ ─æ╞░ß╗úc gß╗¡i ─æi."
                };
            }

            // Check if already verified
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for Email={email}", user.Email);
                throw new BadRequestException("Email ─æ├ú ─æ╞░ß╗úc x├íc minh");
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
                throw new BadRequestException("Kh├┤ng thß╗â tß║ío m├ú x├íc minh. Vui l├▓ng thß╗¡ lß║íi.");
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
                throw new NetworkException("Kh├┤ng thß╗â gß╗¡i email x├íc minh. Vui l├▓ng thß╗¡ lß║íi sau.");
            }

            return new ResendVerificationEmailResponse
            {
                Success = true,
                Message = "Email x├íc minh ─æ├ú ─æ╞░ß╗úc gß╗¡i th├ánh c├┤ng. Vui l├▓ng kiß╗âm tra hß╗Öp th╞░ ─æß║┐n."
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