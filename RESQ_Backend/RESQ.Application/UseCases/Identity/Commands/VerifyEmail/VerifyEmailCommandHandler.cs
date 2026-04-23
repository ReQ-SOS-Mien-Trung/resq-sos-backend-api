using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.VerifyEmail
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
                throw new BadRequestException("M├ú x├íc minh kh├┤ng hß╗úp lß╗ç hoß║╖c ─æ├ú hß║┐t hß║ín");
            }

            // Check if token is expired
            if (user.EmailVerificationTokenExpiry.HasValue && user.EmailVerificationTokenExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Email verification failed: Token expired for Email={email}", user.Email);
                throw new BadRequestException("M├ú x├íc minh ─æ├ú hß║┐t hß║ín. Vui l├▓ng y├¬u cß║ºu gß╗¡i lß║íi email x├íc minh.");
            }

            // Check if already verified
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for Email={email}", user.Email);
                return new VerifyEmailResponse
                {
                    Success = true,
                    Message = "Email ─æ├ú ─æ╞░ß╗úc x├íc minh tr╞░ß╗¢c ─æ├│",
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
                throw new BadRequestException("Kh├┤ng thß╗â x├íc minh email. Vui l├▓ng thß╗¡ lß║íi.");
            }

            _logger.LogInformation("Email verified successfully for Email={email}", user.Email);

            return new VerifyEmailResponse
            {
                Success = true,
                Message = "X├íc minh email th├ánh c├┤ng. Bß║ín c├│ thß╗â ─æ─âng nhß║¡p ngay b├óy giß╗¥.",
                Email = user.Email
            };
        }
    }
}