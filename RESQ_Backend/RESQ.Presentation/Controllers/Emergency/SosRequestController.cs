using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Presentation.Controllers.Emergency;

[Route("api/sos-requests")]
[ApiController]
public class SosRequestController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost]
    [Authorize(Roles = "5")]
    public async Task<IActionResult> Create([FromBody] CreateSosRequestRequestDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        string? structuredDataJson = dto.StructuredData != null
            ? JsonSerializer.Serialize(dto.StructuredData)
            : null;

        string? networkMetadataJson = dto.NetworkMetadata != null
            ? JsonSerializer.Serialize(dto.NetworkMetadata)
            : null;

        var command = new CreateSosRequestCommand(
            userId,
            new GeoLocation(dto.Location.Latitude, dto.Location.Longitude),
            dto.RawMessage,
            dto.PacketId,
            dto.Location.Accuracy,
            dto.SosType,
            structuredDataJson,
            networkMetadataJson,
            dto.Timestamp
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "5")]
    public async Task<IActionResult> GetMySosRequests()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new GetMySosRequestsQuery(userId));
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> GetAllSosRequests()
    {
        var result = await _mediator.Send(new GetAllSosRequestsQuery());
        return Ok(result);
    }

    [HttpGet("paged")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> GetSosRequestsPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = new GetSosRequestsPagedQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "2,5")]
    public async Task<IActionResult> GetSosRequestDetail([FromRoute] int id)
    {
        if (!TryGetUserId(out var userId) || !TryGetRoleId(out var roleId))
            return Unauthorized();

        var result = await _mediator.Send(new GetSosRequestQuery(id, userId, roleId));
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    private bool TryGetRoleId(out int roleId)
    {
        roleId = 0;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        return !string.IsNullOrWhiteSpace(roleClaim) && int.TryParse(roleClaim, out roleId);
    }
}