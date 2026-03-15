using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.CreatePermission;
using RESQ.Application.UseCases.Identity.Commands.DeletePermission;
using RESQ.Application.UseCases.Identity.Commands.UpdatePermission;
using RESQ.Application.UseCases.Identity.Queries.GetPermissions;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/permissions")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class PermissionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy danh sách tất cả permission</summary>
    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var result = await _mediator.Send(new GetPermissionsQuery());
        return Ok(result);
    }

    ///// <summary>Tạo permission mới</summary>
    //[HttpPost]
    //public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequestDto dto)
    //{
    //    var command = new CreatePermissionCommand(dto.Code, dto.Name, dto.Description);
    //    var result = await _mediator.Send(command);
    //    return Ok(result);
    //}

    ///// <summary>Cập nhật permission</summary>
    //[HttpPut("{permissionId:int}")]
    //public async Task<IActionResult> UpdatePermission(int permissionId, [FromBody] UpdatePermissionRequestDto dto)
    //{
    //    var command = new UpdatePermissionCommand(permissionId, dto.Code, dto.Name, dto.Description);
    //    var result = await _mediator.Send(command);
    //    return Ok(result);
    //}

    ///// <summary>Xoá permission</summary>
    //[HttpDelete("{permissionId:int}")]
    //public async Task<IActionResult> DeletePermission(int permissionId)
    //{
    //    await _mediator.Send(new DeletePermissionCommand(permissionId));
    //    return NoContent();
    //}
}
