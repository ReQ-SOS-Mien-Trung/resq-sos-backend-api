using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.GoogleLogin
{
    public class GoogleLoginCommandHandler(
        IUserRepository userRepository,
        IPermissionRepository permissionRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IFirebaseService firebaseService,
        ILogger<GoogleLoginCommandHandler> logger
    ) : IRequestHandler<GoogleLoginCommand, GoogleLoginResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IPermissionRepository _permissionRepository = permissionRepository;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IConfiguration _configuration = configuration;
        private readonly IFirebaseService _firebaseService = firebaseService;
        private readonly ILogger<GoogleLoginCommandHandler> _logger = logger;

        // Default role for rescuer
        private const int DEFAULT_RESCUER_ROLE_ID = 3;

        public async Task<GoogleLoginResponse> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GoogleLoginCommand");

            // Validate Firebase ID Token (issued after Google Sign-In via Firebase)
            var googleUser = await _firebaseService.VerifyGoogleIdTokenAsync(request.IdToken, cancellationToken);

            _logger.LogInformation("Firebase Google token validated for email={email}", googleUser.Email);

            // Check if user exists by email (using username field to store email for Google users)
            var existingUser = await _userRepository.GetByUsernameAsync(googleUser.Email, cancellationToken);

            UserModel user;
            bool isNewUser = false;

            if (existingUser is null)
            {
                // Create new user with rescuer role
                user = new UserModel
                {
                    Id = Guid.NewGuid(),
                    Username = googleUser.Email,
                    FirstName = googleUser.GivenName,
                    LastName = googleUser.FamilyName,
                    Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password for Google users
                    RoleId = DEFAULT_RESCUER_ROLE_ID,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Email = googleUser.Email,
                };

                await _userRepository.CreateAsync(user, cancellationToken);
                isNewUser = true;
                _logger.LogInformation("New rescuer created via Google login: UserId={userId} Email={email}", user.Id, googleUser.Email);
            }
            else
            {
                user = existingUser;
                _logger.LogInformation("Existing user found via Google login: UserId={userId} Email={email}", user.Id, googleUser.Email);
            }

            // Generate tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Get refresh token expiry from configuration
            var refreshTokenExpiryDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");

            // Update user's refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

            if (!isNewUser)
            {
                await _userRepository.UpdateAsync(user, cancellationToken);
            }

            await _unitOfWork.SaveAsync();

            var permissions = await _permissionRepository.GetEffectivePermissionCodesAsync(user.Id, user.RoleId, cancellationToken);

            return new GoogleLoginResponse
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
                IsNewUser = isNewUser,
                RescuerStep = user.RescuerStep
            };
        }
    }
}
