using MediatR;
using System;

namespace RESQ.Application.UseCases.Users.Commands.Logout
{
    public class LogoutCommand : IRequest<Unit>
    {
        public Guid UserId { get; set; }
    }
}
