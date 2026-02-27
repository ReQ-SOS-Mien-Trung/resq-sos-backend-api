using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Commands.LoginRescuer
{
    public class LoginRescuerCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<LoginRescuerCommandHandler> logger
    ) : IRequestHandler<LoginRescuerCommand, LoginRescuerResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<LoginRescuerCommandHandler> _logger = logger;

        // Rescuer role ID
        private const int RESCUER_ROLE_ID = 3;

        public async Task<LoginRescuerResponse> Handle(LoginRescuerCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling LoginRescuerCommand for Email={email}", request.Email);

            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Login failed: User not found for Email={email}", request.Email);
                throw new UnauthorizedException("Thông tin đăng nhập không hợp lệ");
            }

            // Verify this is a rescuer account
            if (user.RoleId != RESCUER_ROLE_ID)
            {
                _logger.LogWarning("Login failed: User is not a rescuer. UserId={userId} RoleId={roleId}", user.Id, user.RoleId);
                throw new UnauthorizedException("Tài khoản không phải là tài khoản rescuer");
            }

            // Verify password
            if (!VerifyPassword(request.Password, user.Password))
            {
                _logger.LogWarning("Login failed: Invalid password for UserId={userId}", user.Id);
                throw new UnauthorizedException("Thông tin đăng nhập không hợp lệ");
            }

            // Check if email is verified
            if (!user.IsEmailVerified)
            {
                _logger.LogWarning("Login failed: Email not verified for UserId={userId}", user.Id);
                throw new ForbiddenException("Email chưa được xác minh. Vui lòng kiểm tra email để xác minh tài khoản.");
            }

            // Generate tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Get refresh token expiry from configuration
            var refreshTokenExpiryDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");

            // Update user's refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Rescuer login successful for UserId={userId} Email={email}", user.Id, user.Email);

            return new LoginRescuerResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60") * 60,
                TokenType = "Bearer",
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RoleId = user.RoleId,
                IsEmailVerified = user.IsEmailVerified,
                IsOnboarded = user.IsOnboarded
            };
        }

        private static bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
        }
    }
}
