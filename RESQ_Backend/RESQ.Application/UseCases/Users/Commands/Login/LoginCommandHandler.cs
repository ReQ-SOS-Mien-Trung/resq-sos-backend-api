using MediatR;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Application.Services;
using RESQ.Domain.Exceptions;
using RESQ.Domain.Repositories;
using System.Threading.Tasks;
using System.Threading;

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
            var user = await _userRepository.GetByUsernameAsync(dto.Username);
            if (user == null)
            {
                throw new InvalidCredentialsException();
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
