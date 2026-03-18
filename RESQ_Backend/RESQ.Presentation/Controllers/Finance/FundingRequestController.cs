using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;
using RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;
using RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;
using RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/funding-requests")]
[ApiController]
public class FundingRequestController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Cách 2] Depot gửi yêu cầu cấp thêm quỹ kèm danh sách vật tư.</summary>
    [HttpPost]
    [Authorize(Roles = "1,4")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateFundingRequestRequest request)
    {
        var command = new CreateFundingRequestCommand(
            request.DepotId,
            request.Description,
            request.AttachmentUrl,
            request.Items.Select(i => new FundingRequestItemDto
            {
                Row          = i.Row,
                ItemName     = i.ItemName,
                CategoryCode = i.CategoryCode,
                Unit         = i.Unit,
                Quantity     = i.Quantity,
                UnitPrice    = i.UnitPrice,
                TotalPrice   = i.TotalPrice,
                ItemType     = i.ItemType,
                TargetGroup  = i.TargetGroup,
                ReceivedDate = i.ReceivedDate,
                ExpiredDate  = i.ExpiredDate,
                Notes        = i.Notes
            }).ToList(),
            GetUserId()
        );

        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    /// <summary>Admin duyệt yêu cầu — chọn campaign để rút tiền.</summary>
    [HttpPatch("{id}/approve")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveFundingRequestRequest request)
    {
        var command = new ApproveFundingRequestCommand(id, request.CampaignId, GetUserId());
        var disbursementId = await _mediator.Send(command);
        return Ok(new { DisbursementId = disbursementId });
    }

    /// <summary>Admin từ chối yêu cầu cấp quỹ.</summary>
    [HttpPatch("{id}/reject")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectFundingRequestRequest request)
    {
        var command = new RejectFundingRequestCommand(id, request.Reason, GetUserId());
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Lấy danh sách yêu cầu cấp quỹ (filter theo depot, status).</summary>
    [HttpGet]
    [Authorize(Roles = "1,4")]
    [ProducesResponseType(typeof(PagedResult<FundingRequestListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? depotId = null,
        [FromQuery] string? status = null)
    {
        var query = new GetFundingRequestsQuery(pageNumber, pageSize, depotId, status);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }
        throw new UnauthorizedAccessException("Invalid User Token");
    }
}
