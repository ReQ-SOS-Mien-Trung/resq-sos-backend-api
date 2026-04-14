using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;
using RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;
using RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;
using RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;

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
    /// Coordinator gom c?m các SOS request thành m?t cluster d? phân tích.
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
    /// L?y danh sách t?t c? cluster hi?n có.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GetClusters()
    {
        var result = await _mediator.Send(new GetSosClustersQuery());
        return Ok(result);
    }

    /// <summary>
    /// Ch?n m?t cluster d? AI phân tích và d? xu?t k? ho?ch nhi?m v? gi?i c?u (blocking).
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

    /// <summary>SSE streaming - AI d? xu?t mission theo th?i gian th?c (event: status | chunk | result | error).</summary>
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
    /// Xem l?i toàn b? mission và activity suggestions mà AI dã d? xu?t cho m?t cluster.
    /// </summary>
    [HttpGet("{clusterId:int}/mission-suggestions")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GetMissionSuggestions([FromRoute] int clusterId)
    {
        var result = await _mediator.Send(new GetMissionSuggestionsQuery(clusterId));
        return Ok(result);
    }

    /// <summary>
    /// G?i ý top 3 kho thay th? d? coordinator b? sung th? công khi kho chính không d? v?t ph?m.
    /// </summary>
    [HttpGet("{clusterId:int}/alternative-depots")]
    [Authorize(Policy = PermissionConstants.PolicySosClusterManage)]
    public async Task<IActionResult> GetAlternativeDepots([FromRoute] int clusterId, [FromQuery] int selectedDepotId)
    {
        var result = await _mediator.Send(new GetAlternativeDepotsQuery(clusterId, selectedDepotId));
        return Ok(result);
    }
}
