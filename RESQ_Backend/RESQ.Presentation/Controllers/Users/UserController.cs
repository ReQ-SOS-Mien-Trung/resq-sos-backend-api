using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Users.Commands.Login;

namespace RESQ.Presentation.Controllers.Users
{
    [Route("api/user")]
    [ApiController]
    public class UserController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
    }
}
