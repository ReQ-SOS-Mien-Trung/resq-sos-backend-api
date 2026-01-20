using MediatR;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Application.Services;
using RESQ.Domain.Models;
using RESQ.Domain.Repositories;
using System.Threading;
using System.Threading.Tasks;

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
            var exists = await _userRepository.GetByUsernameAsync(dto.Username);
            if (exists != null)
            {
                throw new System.Exception("Username already exists");
            }

            var user = new UserModel
            {
                Id = System.Guid.NewGuid(),
                Username = dto.Username,
                FullName = dto.FullName,
                Phone = dto.Phone,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };

            user.Password = RESQ.Application.Services.PasswordHasher.HashPassword(dto.Password);

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
