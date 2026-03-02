using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;
using RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;
using RESQ.Application.UseCases.Emergency.Queries.GetMissionSuggestions;
using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

namespace RESQ.Presentation.Controllers.Emergency;

[Route("emergency/sos-clusters")]
[ApiController]
public class SosClusterController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Coordinator gom cụm các SOS request thành một cluster để phân tích.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "1,2,4")]
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
    /// Lấy danh sách tất cả cluster hiện có.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> GetClusters()
    {
        var result = await _mediator.Send(new GetSosClustersQuery());
        return Ok(result);
    }

    /// <summary>
    /// Chọn một cluster để AI phân tích và đề xuất kế hoạch nhiệm vụ giải cứu.
    /// </summary>
    [HttpPost("{clusterId:int}/rescue-suggestion")]
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> GenerateRescueMissionSuggestion([FromRoute] int clusterId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new GenerateRescueMissionSuggestionCommand(clusterId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Xem lại toàn bộ mission và activity suggestions mà AI đã đề xuất cho một cluster.
    /// </summary>
    [HttpGet("{clusterId:int}/mission-suggestions")]
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> GetMissionSuggestions([FromRoute] int clusterId)
    {
        var result = await _mediator.Send(new GetMissionSuggestionsQuery(clusterId));
        return Ok(result);
    }
}
