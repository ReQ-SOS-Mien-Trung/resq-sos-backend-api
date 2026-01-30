using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Users.Commands.Login;
using RESQ.Application.UseCases.Users.Commands.Logout;
using RESQ.Application.UseCases.Users.Commands.RefreshToken;
using RESQ.Application.UseCases.Users.Commands.Register;

namespace RESQ.Presentation.Controllers.Users
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            var command = new RegisterCommand(dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var command = new LoginCommand(dto.Username, dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var command = new RefreshTokenCommand(dto.AccessToken, dto.RefreshToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new LogoutCommand(userId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
