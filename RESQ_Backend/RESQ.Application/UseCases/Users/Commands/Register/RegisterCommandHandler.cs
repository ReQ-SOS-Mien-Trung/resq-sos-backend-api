using MediatR;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Domain.Entities.Users;
using RESQ.Domain.Entities.Users.Exceptions;
using RESQ.Domain.Repositories;
using RESQ.Application.Exceptions;
using System.Text.RegularExpressions;

namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public RegisterCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Register;
            // Registration only allowed for victims (role 1) using phone and 6-digit PIN
            if (string.IsNullOrWhiteSpace(dto.Phone))
            {
                throw new BadRequestException("Phone is required for registration");
            }

            // PIN must be exactly 6 digits
            if (!Regex.IsMatch(dto.Password ?? string.Empty, "^\\d{6}$"))
            {
                throw new BadRequestException("Password must be a 6-digit PIN");
            }

            var existsByPhone = await _userRepository.GetByPhoneAsync(dto.Phone);
            if (existsByPhone != null)
            {
                throw new UserAlreadyExistsException();
            }

            // If username provided, ensure it's unique as well
            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                var existsByUsername = await _userRepository.GetByUsernameAsync(dto.Username);
                if (existsByUsername != null)
                {
                    throw new UserAlreadyExistsException();
                }
            }

            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Username = dto.Username, // optional, victims typically don't set username
                FullName = dto.FullName,
                Phone = dto.Phone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RoleId = 1
            };

            user.Password = PasswordHasher.HashPassword(dto.Password);

            // create
            await _userRepository.CreateAsync(user);

            // generate tokens
            var auth = await _tokenService.GenerateTokensAsync(user);

            // persist refresh token on user
            user.RefreshToken = auth.RefreshToken;
            user.RefreshTokenExpiry = auth.ExpiresAt;
            await _userRepository.UpdateAsync(user);

            return auth;
        }
    }

    
}
