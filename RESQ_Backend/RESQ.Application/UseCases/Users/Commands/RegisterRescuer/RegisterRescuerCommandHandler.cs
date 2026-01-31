using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Users;
using RESQ.Application.Services;
using RESQ.Domain.Entities;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<RegisterRescuerCommandHandler> logger
    ) : IRequestHandler<RegisterRescuerCommand, RegisterRescuerResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<RegisterRescuerCommandHandler> _logger = logger;

        // Default role for rescuer
        private const int DEFAULT_RESCUER_ROLE_ID = 4;

        public async Task<RegisterRescuerResponse> Handle(RegisterRescuerCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling RegisterRescuerCommand for Email={email}", request.Email);

            // Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existingUser is not null)
            {
                _logger.LogWarning("Registration failed: Email already exists Email={email}", request.Email);
                throw new ConflictException("Email đã được đăng ký");
            }

            // Hash password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Generate email verification token
            var verificationToken = GenerateVerificationToken();
            var tokenExpiry = DateTime.UtcNow.AddHours(24); // Token valid for 24 hours

            // Create new user with rescuer role
            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FullName = request.FullName,
                Password = hashedPassword,
                RoleId = DEFAULT_RESCUER_ROLE_ID,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = tokenExpiry,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new CreateFailedException("Rescuer");
            }

            // Send verification email
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, verificationToken, cancellationToken);
                _logger.LogInformation("Verification email sent to {email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {email}", user.Email);
                // Don't throw - user is created, they can request resend later
            }

            _logger.LogInformation("Rescuer registered successfully: UserId={userId} Email={email}", user.Id, request.Email);

            return new RegisterRescuerResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                RoleId = user.RoleId ?? DEFAULT_RESCUER_ROLE_ID,
                IsEmailVerified = user.IsEmailVerified,
                Message = "Đăng ký thành công. Vui lòng kiểm tra email để xác minh tài khoản của bạn.",
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
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
