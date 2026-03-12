using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Lấy vùng phục vụ đang active (tọa độ polygon hiện tại)
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var result = await _mediator.Send(new GetServiceZoneQuery());
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết vùng phục vụ theo Id
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetServiceZoneByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới vùng phục vụ — Admin vẽ polygon trên bản đồ và gửi danh sách tọa độ.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceZoneRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            return Unauthorized();

        var command = new CreateServiceZoneCommand(
            Name: dto.Name,
            Coordinates: dto.Coordinates,
            IsActive: dto.IsActive,
            CreatedBy: adminId
        );

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Cập nhật vùng phục vụ — Admin vẽ polygon trên bản đồ và gửi danh sách tọa độ.
    /// Tọa độ là danh sách các đỉnh polygon theo thứ tự (clock- hoặc counter-clockwise),
    /// không cần lặp lại điểm đầu ở cuối.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceZoneRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            return Unauthorized();

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
