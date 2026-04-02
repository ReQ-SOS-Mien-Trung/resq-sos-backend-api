using System;
using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteRelativeProfile
{
    public record DeleteRelativeProfileCommand(Guid UserId, Guid ProfileId) : IRequest<Unit>;
}
