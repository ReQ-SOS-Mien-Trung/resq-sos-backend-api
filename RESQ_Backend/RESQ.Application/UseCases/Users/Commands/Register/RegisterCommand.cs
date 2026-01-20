using MediatR;
using RESQ.Application.UseCases.Users.Dtos;

namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public class RegisterCommand : IRequest<AuthResultDto>
    {
        public RegisterDto Register { get; set; } = null!;
    }
}
