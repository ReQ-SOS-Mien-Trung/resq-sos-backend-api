using MediatR;
using RESQ.Domain.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace RESQ.Application.UseCases.Users.Commands.Logout
{
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
    {
        private readonly IUserRepository _userRepository;

        public LogoutCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await _userRepository.UpdateAsync(user);
            }

            return Unit.Value;
        }
    }
}
