using MediatR;
using RESQ.Application.UseCases.Users.Dtos;

namespace RESQ.Application.UseCases.Users.Commands.Login
{
    public class LoginCommand : IRequest<AuthResultDto>
    {
        public LoginDto Login { get; set; } = null!;
    }
}
