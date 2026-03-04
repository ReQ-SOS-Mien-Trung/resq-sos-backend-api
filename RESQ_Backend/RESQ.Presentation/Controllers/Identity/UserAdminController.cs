using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/admin/users")]
    [ApiController]
    [Authorize(Roles = "1")] // Admin only
    public class UserAdminController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>
        /// Admin cập nhật avatar cho một user
        /// </summary>
        /// <param name="userId">ID của user cần cập nhật avatar</param>
        /// <param name="dto">URL avatar mới</param>
        [HttpPut("{userId:guid}/avatar")]
        public async Task<IActionResult> SetUserAvatarUrl(Guid userId, [FromBody] SetUserAvatarUrlRequestDto dto)
        {
            var command = new SetUserAvatarUrlCommand(userId, dto.AvatarUrl);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
