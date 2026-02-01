using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.Login;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("api/user")]
    [ApiController]
    public class UserController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
    }
}
