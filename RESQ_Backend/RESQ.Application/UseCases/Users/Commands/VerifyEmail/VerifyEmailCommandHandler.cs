using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Users;

namespace RESQ.Application.UseCases.Users.Commands.VerifyEmail
{
    public class VerifyEmailCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<VerifyEmailCommandHandler> logger
    ) : IRequestHandler<VerifyEmailCommand, VerifyEmailResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<VerifyEmailCommandHandler> _logger = logger;

        public async Task<VerifyEmailResponse> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling VerifyEmailCommand");

            // Find user by verification token
            var user = await _userRepository.GetByEmailVerificationTokenAsync(request.Token, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Email verification failed: Invalid token");
                throw new BadRequestException("Invalid or expired verification token");
            }

            // Check if token is expired
            if (user.EmailVerificationTokenExpiry.HasValue && user.EmailVerificationTokenExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Email verification failed: Token expired for Email={email}", user.Email);
                throw new BadRequestException("Verification token has expired. Please request a new verification email.");
            }

            // Check if already verified
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for Email={email}", user.Email);
                return new VerifyEmailResponse
                {
                    Success = true,
                    Message = "Email is already verified",
                    Email = user.Email
                };
            }

            // Mark email as verified
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new BadRequestException("Failed to verify email. Please try again.");
            }

            _logger.LogInformation("Email verified successfully for Email={email}", user.Email);

            return new VerifyEmailResponse
            {
                Success = true,
                Message = "Email verified successfully. You can now login.",
                Email = user.Email
            };
        }
    }
}
