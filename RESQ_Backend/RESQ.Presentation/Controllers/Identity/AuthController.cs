using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.GoogleLogin;
using RESQ.Application.UseCases.Identity.Commands.Login;
using RESQ.Application.UseCases.Identity.Commands.Logout;
using RESQ.Application.UseCases.Identity.Commands.RefreshToken;
using RESQ.Application.UseCases.Identity.Commands.Register;
using RESQ.Application.UseCases.Identity.Commands.LoginRescuer;
using RESQ.Application.UseCases.Identity.Commands.RegisterRescuer;
using RESQ.Application.UseCases.Identity.Commands.VerifyEmail;
using RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail;
using RESQ.Application.UseCases.Identity.Commands.ForgotPassword;
using RESQ.Application.UseCases.Identity.Commands.ResetPassword;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/auth")]
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

        [HttpPost("register-rescuer")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterRescuer([FromBody] RegisterRescuerRequestDto dto)
        {
            var command = new RegisterRescuerCommand(dto.Email, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var command = new VerifyEmailCommand(token);
            var result = await _mediator.Send(command);
            if (result.Success)
            {
                return Redirect("http://localhost:5173/verify-email/success");
            }
            return Redirect("http://localhost:5173/auth/resend-email");
        }

        [HttpPost("resend-verification-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequestDto dto)
        {
            var command = new ResendVerificationEmailCommand(dto.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            var command = new ForgotPasswordCommand(dto.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            var command = new ResetPasswordCommand(dto.Token, dto.NewPassword, dto.ConfirmPassword);
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

        [HttpPost("login-rescuer")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginRescuer([FromBody] LoginRescuerRequestDto dto)
        {
            var command = new LoginRescuerCommand(dto.Email, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
        {
            var command = new GoogleLoginCommand(dto.IdToken);
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
