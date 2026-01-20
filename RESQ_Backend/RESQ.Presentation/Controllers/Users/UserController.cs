using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Application.UseCases.Users.Commands.Register;
using RESQ.Application.UseCases.Users.Commands.Login;
using RESQ.Application.UseCases.Users.Commands.Logout;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Users
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var cmd = new RegisterCommand { Register = dto };
            var result = await _mediator.Send(cmd);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var cmd = new LoginCommand { Login = dto };
            var result = await _mediator.Send(cmd);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(sub)) return BadRequest();
            if (!System.Guid.TryParse(sub, out var userId)) return BadRequest();
            var cmd = new LogoutCommand { UserId = userId };
            await _mediator.Send(cmd);
            return NoContent();
        }
    }
}
