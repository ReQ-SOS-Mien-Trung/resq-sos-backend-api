using MediatR;
using RESQ.Application.UseCases.Identity.Commands.Register;

namespace RESQ.Application.UseCases.Identity.Commands.RegisterTest
{
    public record RegisterTestCommand(
        string Phone,
        string Password
    ) : IRequest<RegisterResponse>;
}
