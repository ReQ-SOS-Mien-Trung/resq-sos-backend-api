using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Commands.CancelSosRequest;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;
using RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequestsPaged;
using RESQ.Application.UseCases.Emergency.Queries.GetSosPriorityLevelMetadata;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Presentation.Controllers.Emergency;

[Route("emergency/sos-requests")]
[ApiController]
public class SosRequestController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost]
    [Authorize(Roles = "2,5")]
    public async Task<IActionResult> Create([FromBody] CreateSosRequestRequestDto dto)
    {
        if (dto.SenderInfo?.UserId == null || !Guid.TryParse(dto.SenderInfo.UserId, out var senderUserId))
            return BadRequest(new { message = "sender_info.user_id is required and must be a valid GUID." });

        string? structuredDataJson = dto.StructuredData != null
            ? JsonSerializer.Serialize(dto.StructuredData)
            : null;

        string? networkMetadataJson = dto.NetworkMetadata != null
            ? JsonSerializer.Serialize(dto.NetworkMetadata)
            : null;

        string? senderInfoJson = dto.SenderInfo != null
            ? JsonSerializer.Serialize(dto.SenderInfo)
            : null;

        var command = new CreateSosRequestCommand(
            senderUserId,
            new GeoLocation(dto.Location.Latitude, dto.Location.Longitude),
            dto.RawMessage,
            dto.PacketId,
            dto.OriginId,
            dto.Location.Accuracy,
            dto.SosType,
            structuredDataJson,
            networkMetadataJson,
            senderInfoJson,
            dto.Timestamp,
            CreatedByCoordinatorId: TryGetRoleId(out var callerRoleId) && callerRoleId == 2 && TryGetUserId(out var callerUserId)
                ? callerUserId
                : null,
            ClientCreatedAt: dto.CreatedAt
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

    [HttpGet()]
    [Authorize(Roles = "1,2")]
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
    [Authorize(Roles = "1,2,5")]
    public async Task<IActionResult> GetSosRequestDetail([FromRoute] int id)
    {
        if (!TryGetUserId(out var userId) || !TryGetRoleId(out var roleId))
            return Unauthorized();

        var result = await _mediator.Send(new GetSosRequestQuery(id, userId, roleId));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết đánh giá ưu tiên của một SOS request:
    /// bao gồm điểm rule-based (Medical/Injury/Mobility/Environment/Food + TotalScore + PriorityLevel)
    /// và tất cả bản phân tích AI (nếu đã hoàn thành xử lý bất đồng bộ).
    /// </summary>
    [HttpGet("{id:int}/evaluation")]
    [Authorize(Roles = "1,2,5")]
    public async Task<IActionResult> GetSosEvaluation([FromRoute] int id)
    {
        if (!TryGetUserId(out var userId) || !TryGetRoleId(out var roleId))
            return Unauthorized();

        var result = await _mediator.Send(new GetSosEvaluationQuery(id, userId, roleId));
        return Ok(result);
    }

    /// <summary>
    /// Lấy metadata các mức độ ưu tiên SOS (key tiếng Anh, value tiếng Việt) dùng cho dropdown giao diện.
    /// </summary>
    [HttpGet("metadata/priority-levels")]
    public async Task<IActionResult> GetPriorityLevelMetadata()
    {
        var result = await _mediator.Send(new GetSosPriorityLevelMetadataQuery());
        return Ok(result);
    }

    /// <summary>
    /// Victim huỷ SOS request của mình (chỉ cho phép khi Pending hoặc Assigned).
    /// </summary>
    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "5")]
    public async Task<IActionResult> CancelSosRequest([FromRoute] int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new CancelSosRequestCommand(id, userId));
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