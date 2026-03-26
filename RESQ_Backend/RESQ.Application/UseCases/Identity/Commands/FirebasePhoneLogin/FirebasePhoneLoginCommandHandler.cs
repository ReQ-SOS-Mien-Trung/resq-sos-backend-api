using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.FirebasePhoneLogin;

public class FirebasePhoneLoginCommandHandler(
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<FirebasePhoneLoginCommandHandler> logger
) : IRequestHandler<FirebasePhoneLoginCommand, FirebasePhoneLoginResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<FirebasePhoneLoginCommandHandler> _logger = logger;

    private const int DEFAULT_VICTIM_ROLE_ID = 5;

    public async Task<FirebasePhoneLoginResponse> Handle(FirebasePhoneLoginCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling FirebasePhoneLoginCommand");

        // Verify Firebase ID token
        var tokenInfo = await _firebaseService.VerifyIdTokenAsync(request.IdToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(tokenInfo.Phone))
        {
            throw new BadRequestException("Token không chứa số điện thoại hợp lệ");
        }

        _logger.LogInformation("Firebase token verified. Phone={phone}, Uid={uid}", tokenInfo.Phone, tokenInfo.Uid);

        // Find or create user by phone
        var existingUser = await _userRepository.GetByPhoneAsync(tokenInfo.Phone, cancellationToken);

        UserModel user;
        bool isNewUser = false;

        if (existingUser is null)
        {
            user = new UserModel
            {
                Id = Guid.NewGuid(),
                Username = tokenInfo.Phone,
                Phone = tokenInfo.Phone,
                Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                RoleId = DEFAULT_VICTIM_ROLE_ID,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _userRepository.CreateAsync(user, cancellationToken);
            isNewUser = true;
            _logger.LogInformation("New victim created via Firebase phone login: UserId={userId} Phone={phone}", user.Id, tokenInfo.Phone);
        }
        else
        {
            user = existingUser;
            _logger.LogInformation("Existing user found via Firebase phone login: UserId={userId} Phone={phone}", user.Id, tokenInfo.Phone);
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenExpiryDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        if (!isNewUser)
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        await _unitOfWork.SaveAsync();

        return new FirebasePhoneLoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60") * 60,
            TokenType = "Bearer",
            UserId = user.Id,
            Phone = user.Phone,
            FirstName = user.FirstName,
            LastName = user.LastName,
            RoleId = user.RoleId,
            IsNewUser = isNewUser
        };
    }
}
