using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public class LoginCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<LoginCommandHandler> logger
    ) : IRequestHandler<LoginCommand, LoginResonse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<LoginCommandHandler> _logger = logger;

        public async Task<LoginResonse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling LoginCommand for Username={username} or Phone={phone}", request.Username, request.Phone);

            // Validate input - at least one of username or phone must be provided
            if (string.IsNullOrEmpty(request.Username) && string.IsNullOrEmpty(request.Phone))
            {
                throw new BadRequestException("Tên đăng nhập hoặc số điện thoại là bắt buộc");
            }

            // Find user by username or phone
            var user = !string.IsNullOrEmpty(request.Username)
                ? await _userRepository.GetByUsernameAsync(request.Username, cancellationToken)
                : await _userRepository.GetByPhoneAsync(request.Phone!, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Login failed: User not found for Username={username} or Phone={phone}", request.Username, request.Phone);
                throw new UnauthorizedException("Thông tin đăng nhập không hợp lệ");
            }

            // Verify password
            if (!VerifyPassword(request.Password, user.Password))
            {
                _logger.LogWarning("Login failed: Invalid password for UserId={userId}", user.Id);
                throw new UnauthorizedException("Thông tin đăng nhập không hợp lệ");
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

            _logger.LogInformation("Login successful for UserId={userId}", user.Id);

            return new LoginResonse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60") * 60,
                TokenType = "Bearer",
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                RoleId = user.RoleId
            };
        }

        private static bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            // Using BCrypt for password verification
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
        }
    }
}
