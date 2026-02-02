using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Identity;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Identity.Commands.GoogleLogin
{
    public class GoogleLoginCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleLoginCommandHandler> logger
    ) : IRequestHandler<GoogleLoginCommand, GoogleLoginResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<GoogleLoginCommandHandler> _logger = logger;

        // Default role for rescuer
        private const int DEFAULT_RESCUER_ROLE_ID = 3;

        public async Task<GoogleLoginResponse> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GoogleLoginCommand");

            // Validate Google ID Token
            var googleUser = await ValidateGoogleTokenAsync(request.IdToken, cancellationToken);
            if (googleUser is null)
            {
                throw new UnauthorizedException("Token Google không hợp lệ");
            }

            _logger.LogInformation("Google token validated for email={email}", googleUser.Email);

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
                    FullName = googleUser.Name,
                    Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password for Google users
                    RoleId = DEFAULT_RESCUER_ROLE_ID,
                    IsOnboarded = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
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

            return new GoogleLoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60") * 60,
                TokenType = "Bearer",
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                RoleId = user.RoleId,
                IsNewUser = isNewUser,
                IsOnboarded = user.IsOnboarded
            };
        }

        private async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string idToken, CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(
                    $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google token validation failed with status code: {statusCode}", response.StatusCode);
                    return null;
                }

                var tokenInfo = await response.Content.ReadFromJsonAsync<GoogleTokenInfo>(cancellationToken);
                
                if (tokenInfo is null)
                {
                    return null;
                }

                // Verify the token is for our app
                var clientId = _configuration["GoogleAuth:ClientId"];
                if (tokenInfo.Aud != clientId)
                {
                    _logger.LogWarning("Google token client ID mismatch. Expected: {expected}, Got: {actual}", clientId, tokenInfo.Aud);
                    return null;
                }

                return new GoogleUserInfo
                {
                    Email = tokenInfo.Email,
                    Name = tokenInfo.Name,
                    Picture = tokenInfo.Picture,
                    EmailVerified = tokenInfo.EmailVerified == "true"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Google token");
                return null;
            }
        }
    }

    public class GoogleTokenInfo
    {
        [JsonPropertyName("aud")]
        public string? Aud { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; } = null!;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }

        [JsonPropertyName("email_verified")]
        public string? EmailVerified { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; } = null!;
        public string? Name { get; set; }
        public string? Picture { get; set; }
        public bool EmailVerified { get; set; }
    }
}
