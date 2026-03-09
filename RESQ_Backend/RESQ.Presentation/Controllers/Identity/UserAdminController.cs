using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;
using RESQ.Application.UseCases.Identity.Commands.AdminUpdateUser;
using RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;
using RESQ.Application.UseCases.Identity.Commands.BanUser;
using RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl;
using RESQ.Application.UseCases.Identity.Commands.UnbanUser;
using RESQ.Application.UseCases.Identity.Queries.GetUserById;
using RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;
using RESQ.Application.UseCases.Identity.Queries.GetUsers;
using RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/admin/users")]
    [ApiController]
    [Authorize(Roles = "1")] // Admin only
    public class UserAdminController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách user có phân trang, lọc theo role / trạng thái ban / từ khóa</summary>
        [HttpGet]
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

        /// <summary>Lấy thông tin chi tiết một user theo ID</summary>
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var query = new GetUserByIdQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Admin tạo tài khoản mới với role chỉ định</summary>
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto dto)
        {
            var command = new AdminCreateUserCommand(
                dto.Phone, dto.Email, dto.FirstName, dto.LastName,
                dto.Username, dto.Password, dto.RoleId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin cập nhật thông tin user</summary>
        [HttpPut("{userId:guid}")]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] AdminUpdateUserRequestDto dto)
        {
            var command = new AdminUpdateUserCommand(
                userId, dto.FirstName, dto.LastName, dto.Username,
                dto.Phone, dto.Email, dto.RescuerType, dto.RoleId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin ban một user</summary>
        [HttpPost("{userId:guid}/ban")]
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
        public async Task<IActionResult> UnbanUser(Guid userId)
        {
            var command = new UnbanUserCommand(userId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin gán role cho user</summary>
        [HttpPut("{userId:guid}/role")]
        public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleToUserRequestDto dto)
        {
            var command = new AssignRoleToUserCommand(userId, dto.RoleId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Admin cập nhật avatar cho một user</summary>
        [HttpPut("{userId:guid}/avatar")]
        public async Task<IActionResult> SetUserAvatarUrl(Guid userId, [FromBody] SetUserAvatarUrlRequestDto dto)
        {
            var command = new SetUserAvatarUrlCommand(userId, dto.AvatarUrl);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Lấy danh sách permission trực tiếp của user</summary>
        [HttpGet("{userId:guid}/permissions")]
        public async Task<IActionResult> GetUserPermissions(Guid userId)
        {
            var query = new GetUserPermissionsQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Gán/thay thế danh sách permission trực tiếp cho user</summary>
        [HttpPut("{userId:guid}/permissions")]
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
