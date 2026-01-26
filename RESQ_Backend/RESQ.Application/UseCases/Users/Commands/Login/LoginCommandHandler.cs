using MediatR;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Application.Services;
using RESQ.Domain.Exceptions;
using RESQ.Domain.Repositories;
using RESQ.Domain.Entities.Users;
using System.Threading.Tasks;
using System.Threading;
using RESQ.Application.Exceptions;

namespace RESQ.Application.UseCases.Users.Commands.Login
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public LoginCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Login;
            if (string.IsNullOrWhiteSpace(dto.Username) && string.IsNullOrWhiteSpace(dto.Phone))
            {
                throw new BadRequestException("Username or phone is required");
            }

            UserModel? user = null;
            if (!string.IsNullOrWhiteSpace(dto.Phone))
            {
                user = await _userRepository.GetByPhoneAsync(dto.Phone!);
            }
            else if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                user = await _userRepository.GetByUsernameAsync(dto.Username!);
            }

            if (user == null)
            {
                throw new InvalidCredentialsException();
            }

            // Enforce login method per role:
            // - Victim (role 1) must login using phone
            // - Other roles must login using username
            if (user.RoleId == 1)
            {
                if (string.IsNullOrWhiteSpace(dto.Phone))
                {
                    throw new BadRequestException("Victims must login using phone number");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.Username))
                {
                    throw new BadRequestException("This account must login using username");
                }
            }

            var ok = PasswordHasher.Verify(user.Password, dto.Password);
            if (!ok) throw new InvalidCredentialsException();

            var auth = await _tokenService.GenerateTokensAsync(user);

            // persist refresh token
            user.RefreshToken = auth.RefreshToken;
            user.RefreshTokenExpiry = auth.ExpiresAt;
            await _userRepository.UpdateAsync(user);

            return auth;
        }
    }
}
