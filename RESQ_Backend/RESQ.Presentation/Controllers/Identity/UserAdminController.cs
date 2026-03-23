using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;
using RESQ.Application.UseCases.Identity.Commands.AdminUpdateUser;
using RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;
using RESQ.Application.UseCases.Identity.Commands.BanUser;
using RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl;
using RESQ.Application.UseCases.Identity.Commands.UnbanUser;
using RESQ.Application.UseCases.Identity.Queries.GetUserById;
using RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;
using RESQ.Application.UseCases.Identity.Queries.GetRescuers;
using RESQ.Application.UseCases.Identity.Queries.GetUsersForPermission;
using RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/admin/users")]
    [ApiController]
    [Authorize]
    public class UserAdminController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách user có phân trang (không bao gồm Rescuer)</summary>
        [HttpGet]
        [Authorize(Policy = PermissionConstants.SystemUserView)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? roleId = null,
            [FromQuery] bool? isBanned = null,
            [FromQuery] string? search = null)
        {
            var query = new GetUsersQuery(pageNumber, pageSize, roleId, isBanned, search);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách user cho trang phân quyền admin.
        /// Loại trừ: user bị ban và volunteer chưa kích hoạt (IsEligibleRescuer=false VÀ IsOnboarded=false).
        /// </summary>
        [HttpGet("for-permission")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> GetUsersForPermission(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? roleId = null,
            [FromQuery] string? search = null)
        {
            var query = new GetUsersForPermissionQuery(pageNumber, pageSize, roleId, search);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Lấy danh sách Rescuer đủ điều kiện (RoleId=3, IsEligibleRescuer=true) - thông tin cơ bản, không kèm abilities/chứng chỉ</summary>
        [HttpGet("rescuers")]
        [Authorize(Policy = PermissionConstants.SystemUserView)]
        public async Task<IActionResult> GetRescuers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isBanned = null,
            [FromQuery] string? search = null)
        {
            var query = new GetRescuersQuery(pageNumber, pageSize, isBanned, search);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Lấy thông tin chi tiết một user theo ID</summary>
        [HttpGet("{userId:guid}")]
        [Authorize(Policy = PermissionConstants.SystemUserView)]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var query = new GetUserByIdQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Admin tạo tài khoản mới với role chỉ định</summary>
        [HttpPost]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto dto)
        {
            var command = new AdminCreateUserCommand(
                dto.Phone, dto.Email, dto.FirstName, dto.LastName,
                dto.Username, dto.Password, dto.RoleId,
                dto.RescuerType, dto.AvatarUrl, dto.Address, dto.Ward, dto.Province,
                dto.Latitude, dto.Longitude,
                dto.IsEmailVerified, dto.IsOnboarded, dto.IsEligibleRescuer,
                dto.ApprovedBy, dto.ApprovedAt);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin cập nhật thông tin user</summary>
        [HttpPut("{userId:guid}")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] AdminUpdateUserRequestDto dto)
        {
            var command = new AdminUpdateUserCommand(
                userId, dto.FirstName, dto.LastName, dto.Username,
                dto.Phone, dto.Email, dto.RescuerType, dto.RoleId,
                dto.AvatarUrl, dto.Address, dto.Ward, dto.Province,
                dto.Latitude, dto.Longitude,
                dto.IsEmailVerified, dto.IsOnboarded, dto.IsEligibleRescuer,
                dto.ApprovedBy, dto.ApprovedAt);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin ban một user</summary>
        [HttpPost("{userId:guid}/ban")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> BanUser(Guid userId, [FromBody] BanUserRequestDto dto)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var command = new BanUserCommand(userId, adminId, dto.Reason);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin gỡ ban một user</summary>
        [HttpPost("{userId:guid}/unban")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> UnbanUser(Guid userId)
        {
            var command = new UnbanUserCommand(userId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin gán role cho user</summary>
        [HttpPut("{userId:guid}/role")]
        [Authorize(Policy = PermissionConstants.SystemConfigManage)]
        public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleToUserRequestDto dto)
        {
            var command = new AssignRoleToUserCommand(userId, dto.RoleId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin cập nhật avatar cho một user</summary>
        [HttpPut("{userId:guid}/avatar")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
        public async Task<IActionResult> SetUserAvatarUrl(Guid userId, [FromBody] SetUserAvatarUrlRequestDto dto)
        {
            var command = new SetUserAvatarUrlCommand(userId, dto.AvatarUrl);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Lấy danh sách permission trực tiếp của user</summary>
        [HttpGet("{userId:guid}/permissions")]
        [Authorize(Policy = PermissionConstants.SystemConfigManage)]
        public async Task<IActionResult> GetUserPermissions(Guid userId)
        {
            var query = new GetUserPermissionsQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Gán/thay thế danh sách permission trực tiếp cho user</summary>
        [HttpPut("{userId:guid}/permissions")]
        [Authorize(Policy = PermissionConstants.SystemConfigManage)]
        public async Task<IActionResult> SetUserPermissions(Guid userId, [FromBody] SetUserPermissionsRequestDto dto)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var command = new SetUserPermissionsCommand(userId, adminId, dto.PermissionIds);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
