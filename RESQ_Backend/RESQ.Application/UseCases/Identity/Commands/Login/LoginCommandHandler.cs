using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public class LoginCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
        IUserRepository userRepository,
        IPermissionRepository permissionRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<LoginCommandHandler> logger,
        IDepotInventoryRepository depotInventoryRepository,
        IDepotRepository depotRepository
    ) : IRequestHandler<LoginCommand, LoginResonse>
    {
        private readonly IUserRepository _userRepository = userRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
        private readonly IPermissionRepository _permissionRepository = permissionRepository;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<LoginCommandHandler> _logger = logger;
        private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
        private readonly IDepotRepository _depotRepository = depotRepository;

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

            int? depotId = null;
            string? depotName = null;
            int? managedDepotId = null;

            if (user.RoleId == RoleConstants.Manager)
            {
                managedDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(user.Id, cancellationToken);
                if (!managedDepotId.HasValue)
                {
                    _logger.LogWarning(
                        "Login blocked: Depot manager has no active depot assignment for UserId={userId}",
                        user.Id);

                    throw ExceptionCodes.WithCode(
                        new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                        LogisticsErrorCodes.DepotManagerNotAssigned);
                }
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

            var permissions = await _permissionRepository.GetEffectivePermissionCodesAsync(user.Id, user.RoleId, cancellationToken);

            managedDepotId ??= await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(user.Id, cancellationToken);
            if (managedDepotId.HasValue)
            {
                depotId = managedDepotId.Value;
                var depot = await _depotRepository.GetByIdAsync(managedDepotId.Value, cancellationToken);
                depotName = depot?.Name;
            }

            return new LoginResonse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "10080") * 60,
                TokenType = "Bearer",
                UserId = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RoleId = user.RoleId,
                Permissions = permissions,
                DepotId = depotId,
                DepotName = depotName
            };
        }

        private static bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            // Using BCrypt for password verification
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
        }
    }
}
