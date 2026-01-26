using MediatR;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Domain.Entities.Users;
using RESQ.Domain.Entities.Users.Exceptions;
using RESQ.Domain.Repositories;

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
                throw new UserAlreadyExistsException();
            }

            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
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
