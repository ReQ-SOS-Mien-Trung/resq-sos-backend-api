using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateServiceZone;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;
using RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

namespace RESQ.Presentation.Controllers.System;

[Route("system/service-zone")]
[ApiController]
[Authorize]
public class ServiceZoneController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Xem tất cả vùng phục vụ.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllServiceZoneQuery());
        return Ok(result);
    }

    /// <summary>Xem danh sách vùng phục vụ đang active.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var result = await _mediator.Send(new GetServiceZoneQuery());
        return Ok(result);
    }

    /// <summary>Xem chi tiết vùng phục vụ theo ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetServiceZoneByIdQuery(id));
        return Ok(result);
    }

    /// <summary>Tạo vùng phục vụ mới (Admin vẽ polygon trên bản đồ và gửi toạ độ).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceZoneRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new CreateServiceZoneCommand(
            Name: dto.Name,
            Coordinates: dto.Coordinates,
            IsActive: dto.IsActive,
            CreatedBy: adminId
        );

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Cập nhật vùng phục vụ (Admin vẽ lại polygon và gửi toạ độ mới).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceZoneRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new UpdateServiceZoneCommand(
            Id: id,
            Name: dto.Name,
            Coordinates: dto.Coordinates,
            IsActive: dto.IsActive,
            UpdatedBy: adminId
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
