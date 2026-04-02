using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerTypeMetadata;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/user")]
    [ApiController]
    public class UserController(IMediator mediator, IFirebaseService firebaseService) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
        private readonly IFirebaseService _firebaseService = firebaseService;

        /// <summary>Lấy thông tin người dùng hiện tại từ token.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var query = new GetCurrentUserQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách loại rescuer (Core, Volunteer).</summary>
        [HttpGet("rescuer/metadata/types")]
        public async Task<IActionResult> GetRescuerTypeMetadata()
        {
            var query = new GetRescuerTypeMetadataQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Rescuer xác nhận đồng ý các điều khoản tham gia.</summary>
        [HttpPost("rescuer/consent")]
        [Authorize]
        public async Task<IActionResult> RescuerConsent([FromBody] RescuerConsentRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new RescuerConsentCommand(
                userId,
                dto.AgreeMedicalFitness,
                dto.AgreeLegalResponsibility,
                dto.AgreeTraining,
                dto.AgreeCodeOfConduct
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Cập nhật thông tin hồ sơ rescuer.</summary>
        [HttpPut("rescuer/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateRescuerProfile([FromBody] UpdateRescuerProfileRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new UpdateRescuerProfileCommand(
                userId,
                dto.FirstName,
                dto.LastName,
                dto.Phone,
                dto.Address,
                dto.Ward,
                dto.Province,
                dto.Latitude,
                dto.Longitude,
                dto.AvatarUrl
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Đăng ký FCM device token (web) vào topic của user hiện tại.
        /// Gọi sau khi login thành công trên Next.js.
        /// </summary>
        [HttpPost("me/fcm-token")]
        [Authorize]
        public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _firebaseService.SubscribeToUserTopicAsync(request.Token, userId, HttpContext.RequestAborted);
            return Ok(new { message = "FCM token registered successfully." });
        }

        /// <summary>
        /// Hủy đăng ký FCM device token (web) khỏi topic của user hiện tại.
        /// Gọi khi logout trên Next.js.
        /// </summary>
        [HttpDelete("me/fcm-token")]
        [Authorize]
        public async Task<IActionResult> UnregisterFcmToken([FromBody] FcmTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _firebaseService.UnsubscribeFromUserTopicAsync(request.Token, userId, HttpContext.RequestAborted);
            return Ok(new { message = "FCM token unregistered successfully." });
        }
    }
}

public record FcmTokenRequest(string Token);