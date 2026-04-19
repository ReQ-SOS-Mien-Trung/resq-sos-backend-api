using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.SetRolePermissions;
using RESQ.Application.UseCases.Identity.Queries.GetRoleMetadata;
using RESQ.Application.UseCases.Identity.Queries.GetRolePermissions;
using RoleConsts = RESQ.Application.Common.Constants.RoleConstants;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/roles")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class RolesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Metadata] Danh sách role dùng cho dropdown (key = id, value = tên).</summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        var result = await _mediator.Send(new GetRoleMetadataQuery());
        return Ok(result);
    }

    ///// <summary>Lấy danh sách tất cả role</summary>
    //[HttpGet]
    //public async Task<IActionResult> GetRoles()
    //{
    //    var result = await _mediator.Send(new GetRolesQuery());
    //    return Ok(result);
    //}

    /// <summary>Lấy danh sách permission của một role</summary>
    [HttpGet("{roleId:int}/permissions")]
    public async Task<IActionResult> GetRolePermissions(int roleId)
    {
        var result = await _mediator.Send(new GetRolePermissionsQuery(roleId));
        return Ok(result);
    }

    /// <summary>Lấy danh sách permission của Depot Manager (dành cho Admin xem)</summary>
    [HttpGet("depot-manager/permissions")]
    public async Task<IActionResult> GetDepotManagerPermissions()
    {
        var result = await _mediator.Send(new GetRolePermissionsQuery(RoleConsts.Manager));
        return Ok(result);
    }

    ///// <summary>Tạo role mới</summary>
    //[HttpPost]
    //public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequestDto dto)
    //{
    //    var command = new CreateRoleCommand(dto.Name);
    //    var result = await _mediator.Send(command);
    //    return Ok(result);
    //}

    ///// <summary>Cập nhật tên role</summary>
    //[HttpPut("{roleId:int}")]
    //public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleRequestDto dto)
    //{
    //    var command = new UpdateRoleCommand(roleId, dto.Name);
    //    var result = await _mediator.Send(command);
    //    return Ok(result);
    //}

    ///// <summary>Xoá role</summary>
    //[HttpDelete("{roleId:int}")]
    //public async Task<IActionResult> DeleteRole(int roleId)
    //{
    //    await _mediator.Send(new DeleteRoleCommand(roleId));
    //    return NoContent();
    //}

    /// <summary>Gán/thay thế danh sách permission cho role</summary>
    [HttpPut("{roleId:int}/permissions")]
    public async Task<IActionResult> SetRolePermissions(int roleId, [FromBody] SetRolePermissionsRequestDto dto)
    {
        var command = new SetRolePermissionsCommand(roleId, dto.PermissionIds);
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
