using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;
using RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;
using RESQ.Application.UseCases.Finance.Queries.GetCampaignDisbursements;
using RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/disbursements")]
[ApiController]
public class CampaignDisbursementController(IMediator mediator, IAuthorizationService authorizationService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Cách 1] Admin chủ động cấp tiền từ Campaign → Depot.</summary>
    [HttpPost("allocate")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AllocateFundToDepot([FromBody] AllocateFundToDepotRequest request)
    {
        var command = new AllocateFundToDepotCommand(
            request.FundCampaignId,
            request.DepotId,
            request.Amount,
            request.Purpose,
            GetUserId()
        );

        var disbursementId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetDisbursements), new { id = disbursementId }, disbursementId);
    }

    /// <summary>
    /// [DepotManager] Báo cáo vật tư đã mua sau khi nhận tiền — công khai cho donor xem.
    /// Admin cũng có thể thêm để hỗ trợ.
    /// </summary>
    [HttpPost("{id}/items")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItems(int id, [FromBody] AddDisbursementItemsRequest request)
    {
        var canManageAnyDisbursement = (await authorizationService
            .AuthorizeAsync(User, null, PermissionConstants.SystemConfigManage))
            .Succeeded;

        var command = new AddDisbursementItemsCommand(
            id,
            request.Items.Select(i => new DisbursementItemDto
            {
                ItemName = i.ItemName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Note = i.Note
            }).ToList(),
            GetUserId(),
            canManageAnyDisbursement
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Lấy danh sách giải ngân (có phân trang, filter theo campaign/depot).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(PagedResult<CampaignDisbursementListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisbursements(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? campaignId = null,
        [FromQuery] int? depotId = null)
    {
        var query = new GetCampaignDisbursementsQuery(pageNumber, pageSize, campaignId, depotId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Công khai] Donor xem tiền campaign đã được dùng mua vật tư gì.</summary>
    [HttpGet("public/campaigns/{campaignId}/spending")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicCampaignSpendingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSpending(
        int campaignId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetPublicCampaignSpendingQuery(campaignId, pageNumber, pageSize);
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
