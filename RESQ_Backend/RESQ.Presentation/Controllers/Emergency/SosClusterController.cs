using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;
using RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;
using RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;
using RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Presentation.Controllers.Emergency;

[Route("emergency/sos-clusters")]
[ApiController]
public class SosClusterController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    private static readonly JsonSerializerOptions _sseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Coordinator gom cụm các SOS request thành một cluster để phân tích.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> CreateCluster([FromBody] CreateSosClusterRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new CreateSosClusterCommand(dto.SosRequestIds, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Coordinator tách một SOS request ra khỏi cluster hiện tại.
    /// </summary>
    [HttpDelete("{clusterId:int}/sos-requests/{sosRequestId:int}")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    [ProducesResponseType(typeof(RemoveSosRequestFromClusterResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveSosRequestFromCluster([FromRoute] int clusterId, [FromRoute] int sosRequestId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new RemoveSosRequestFromClusterCommand(clusterId, sosRequestId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách tất cả cluster hiện có.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    [ProducesResponseType(typeof(PagedResult<SosClusterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClusters(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? sosRequestId = null,
        [FromQuery] List<SosClusterStatus>? statuses = null)
    {
        var result = await _mediator.Send(new GetSosClustersQuery(pageNumber, pageSize, sosRequestId, statuses));
        return Ok(result);
    }

    /// <summary>
    /// Chọn một cluster để AI phân tích và đề xuất kế hoạch nhiệm vụ giải cứu (blocking).
    /// </summary>
    [HttpPost("{clusterId:int}/rescue-suggestion")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GenerateRescueMissionSuggestion([FromRoute] int clusterId)
    {
       var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
       if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
           return Unauthorized();

       var command = new GenerateRescueMissionSuggestionCommand(clusterId, userId);
       var result = await _mediator.Send(command);
       return Ok(result);
    }

    /// <summary>SSE streaming - AI đề xuất mission theo thời gian thực (event: status | chunk | result | error).</summary>
    [HttpGet("{clusterId:int}/rescue-suggestion/stream")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task StreamRescueMissionSuggestion([FromRoute] int clusterId, CancellationToken cancellationToken)
    {
        Response.Headers["Content-Type"]      = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"]     = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"]        = "keep-alive";

        await foreach (var evt in _mediator.CreateStream(
            new StreamRescueMissionSuggestionQuery(clusterId), cancellationToken))
        {
            var json = JsonSerializer.Serialize(evt, _sseJsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Xem lại toàn bộ mission và activity suggestions mà AI đã đề xuất cho một cluster.
    /// </summary>
    [HttpGet("{clusterId:int}/mission-suggestions")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GetMissionSuggestions([FromRoute] int clusterId)
    {
        var result = await _mediator.Send(new GetMissionSuggestionsQuery(clusterId));
        return Ok(result);
    }

    /// <summary>
    /// Gợi ý top 3 kho thay thế để coordinator bổ sung thủ công khi kho chính không đủ vật phẩm.
    /// </summary>
    [HttpGet("{clusterId:int}/alternative-depots")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GetAlternativeDepots([FromRoute] int clusterId, [FromQuery] int selectedDepotId)
    {
        var result = await _mediator.Send(new GetAlternativeDepotsQuery(clusterId, selectedDepotId));
        return Ok(result);
    }
}
