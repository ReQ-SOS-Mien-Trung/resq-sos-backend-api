using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.GoogleLogin;
using RESQ.Application.UseCases.Identity.Commands.Login;
using RESQ.Application.UseCases.Identity.Commands.Logout;
using RESQ.Application.UseCases.Identity.Commands.RefreshToken;
using RESQ.Application.UseCases.Identity.Commands.Register;
using RESQ.Application.UseCases.Identity.Commands.RegisterTest;
using RESQ.Application.UseCases.Identity.Commands.LoginRescuer;
using RESQ.Application.UseCases.Identity.Commands.RegisterRescuer;
using RESQ.Application.UseCases.Identity.Commands.VerifyEmail;
using RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail;
using RESQ.Application.UseCases.Identity.Commands.ForgotPassword;
using RESQ.Application.UseCases.Identity.Commands.ResetPassword;
using RESQ.Application.UseCases.Identity.Commands.FirebasePhoneLogin;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/auth")]
    [ApiController]
    public class AuthController(IMediator mediator, IConfiguration configuration) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
        private readonly string _feBaseUrl = configuration["AppSettings:FEBaseUrl"]?.TrimEnd('/') ?? "https://resq-sos-mientrung.vercel.app";

        /// <summary>Đăng ký tài khoản Victim bằng số điện thoại.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            var command = new RegisterCommand(dto.Phone, dto.Password, dto.FirebaseIdToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng ký tài khoản Rescuer bằng email.</summary>
        [HttpPost("register-rescuer")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterRescuer([FromBody] RegisterRescuerRequestDto dto)
        {
            var command = new RegisterRescuerCommand(dto.Email, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng ký tài khoản test (bỏ qua xác thực Firebase).</summary>
        [HttpPost("register-test")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterTest([FromBody] RegisterTestRequestDto dto)
        {
            var command = new RegisterTestCommand(dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xác thực email qua link gửi về hộp thư (redirect về FE sau khi xử lý).</summary>
        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var command = new VerifyEmailCommand(token);
            var result = await _mediator.Send(command);
            if (result.Success)
            {
                return Redirect($"{_feBaseUrl}/verify-email/success");
            }
            return Redirect($"{_feBaseUrl}/auth/resend-email");
        }

        /// <summary>Gửi lại email xác thực tài khoản.</summary>
        [HttpPost("resend-verification-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequestDto dto)
        {
            var command = new ResendVerificationEmailCommand(dto.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Gửi email đặt lại mật khẩu.</summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            var command = new ForgotPasswordCommand(dto.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đặt lại mật khẩu bằng token nhận qua email.</summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            var command = new ResetPasswordCommand(dto.Token, dto.NewPassword, dto.ConfirmPassword);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng nhập bằng username/phone + mật khẩu.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var command = new LoginCommand(dto.Username, dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng nhập dành cho Rescuer bằng email + mật khẩu.</summary>
        [HttpPost("login-rescuer")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginRescuer([FromBody] LoginRescuerRequestDto dto)
        {
            var command = new LoginRescuerCommand(dto.Email, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng nhập bằng Firebase Phone Auth (IdToken từ SDK Firebase).</summary>
        [HttpPost("firebase-phone-login")]
        [AllowAnonymous]
        public async Task<IActionResult> FirebasePhoneLogin([FromBody] FirebasePhoneLoginRequestDto dto)
        {
            var command = new FirebasePhoneLoginCommand(dto.IdToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng nhập bằng Google OAuth (IdToken từ SDK Google).</summary>
        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
        {
            var command = new GoogleLoginCommand(dto.IdToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Làm mới access token bằng refresh token.</summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var command = new RefreshTokenCommand(dto.AccessToken, dto.RefreshToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Đăng xuất và thu hồi refresh token.</summary>
        [HttpPost("logout")]
        [Authorize(Policy = PermissionConstants.IdentitySessionManage)]
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
