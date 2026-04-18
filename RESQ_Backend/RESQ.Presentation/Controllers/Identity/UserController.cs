using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile;
using RESQ.Application.UseCases.Identity.Commands.DeleteRelativeProfile;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles;
using RESQ.Application.UseCases.Identity.Commands.UpdateRelativeProfile;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;
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
        [Authorize(Policy = PermissionConstants.IdentitySelfView)]
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
        [Authorize(Policy = PermissionConstants.IdentityProfileUpdate)]
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

        /// <summary>Cập nhật thông tin hồ sơ.</summary>
        [HttpPut("profile")]
        [Authorize(Policy = PermissionConstants.IdentityProfileUpdate)]
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
        [Authorize(Policy = PermissionConstants.IdentityNotificationDeviceManage)]
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
        [Authorize(Policy = PermissionConstants.IdentityNotificationDeviceManage)]
        public async Task<IActionResult> UnregisterFcmToken([FromBody] FcmTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _firebaseService.UnsubscribeFromUserTopicAsync(request.Token, userId, HttpContext.RequestAborted);
            return Ok(new { message = "FCM token unregistered successfully." });
        }

        // ---------------------------------------------------------------------
        // Relative Profiles
        // ---------------------------------------------------------------------

        /// <summary>Lấy danh sách hồ sơ người thân của user hiện tại.</summary>
        [HttpGet("me/relative-profiles")]
        [Authorize(Policy = PermissionConstants.IdentityRelativeProfileView)]
        public async Task<IActionResult> GetRelativeProfiles()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var result = await _mediator.Send(new GetRelativeProfilesQuery(userId));
            return Ok(result);
        }

        /// <summary>Tạo một hồ sơ người thân mới.</summary>
        [HttpPost("me/relative-profiles")]
        [Authorize(Policy = PermissionConstants.IdentityRelativeProfileManage)]
        public async Task<IActionResult> CreateRelativeProfile([FromBody] CreateRelativeProfileRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var medicalProfileJson = dto.MedicalProfile != null
                ? JsonSerializer.Serialize(dto.MedicalProfile)
                : null;

            var command = new CreateRelativeProfileCommand(
                userId,
                dto.Id,
                dto.DisplayName,
                dto.PhoneNumber,
                dto.PersonType,
                dto.RelationGroup,
                dto.Tags,
                dto.MedicalBaselineNote,
                dto.SpecialNeedsNote,
                dto.SpecialDietNote,
                dto.Gender,
                medicalProfileJson,
                dto.UpdatedAt
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Cập nhật toàn bộ hồ sơ người thân.</summary>
        [HttpPut("me/relative-profiles/{profileId:guid}")]
        [Authorize(Policy = PermissionConstants.IdentityRelativeProfileManage)]
        public async Task<IActionResult> UpdateRelativeProfile(
            Guid profileId,
            [FromBody] UpdateRelativeProfileRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            if (dto.Id.HasValue && dto.Id.Value != profileId)
                return BadRequest(new { message = "id trong body không khớp với profileId trên đường dẫn." });

            var medicalProfileJson = dto.MedicalProfile != null
                ? JsonSerializer.Serialize(dto.MedicalProfile)
                : null;

            var command = new UpdateRelativeProfileCommand(
                userId,
                profileId,
                dto.DisplayName,
                dto.PhoneNumber,
                dto.PersonType,
                dto.RelationGroup,
                dto.Tags,
                dto.MedicalBaselineNote,
                dto.SpecialNeedsNote,
                dto.SpecialDietNote,
                dto.Gender,
                medicalProfileJson,
                dto.UpdatedAt
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xóa hồ sơ người thân.</summary>
        [HttpDelete("me/relative-profiles/{profileId:guid}")]
        [Authorize(Policy = PermissionConstants.IdentityRelativeProfileManage)]
        public async Task<IActionResult> DeleteRelativeProfile(Guid profileId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _mediator.Send(new DeleteRelativeProfileCommand(userId, profileId));
            return NoContent();
        }

        /// <summary>Đồng bộ toàn bộ snapshot hồ sơ người thân từ thiết bị lên server (client-wins).</summary>
        [HttpPut("me/relative-profiles/sync")]
        [Authorize(Policy = PermissionConstants.IdentityRelativeProfileManage)]
        public async Task<IActionResult> SyncRelativeProfiles([FromBody] SyncRelativeProfilesRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var command = new SyncRelativeProfilesCommand(userId, dto.Profiles);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}

public record FcmTokenRequest(string Token);
