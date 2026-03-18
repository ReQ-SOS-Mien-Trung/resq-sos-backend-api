using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;
using RESQ.Application.UseCases.Finance.Commands.CreateCampaign;
using RESQ.Application.UseCases.Finance.Commands.DeleteCampaign;
using RESQ.Application.UseCases.Finance.Commands.ExtendCampaign;
using RESQ.Application.UseCases.Finance.Commands.IncreaseTargetAmount;
using RESQ.Application.UseCases.Finance.Commands.UpdateCampaignInfo;
using RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;
using RESQ.Application.UseCases.Finance.Queries.ViewCampaignMetadata;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/campaigns")]
[ApiController]
public class FundCampaignController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>L?y danh sách chi?n d?ch gây qu? có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CampaignListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = new ViewAllCampaignsQuery(pageNumber, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách chi?n d?ch dang ho?t d?ng dùng cho dropdown.</summary>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata()
    {
        var query = new ViewCampaignMetadataQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>T?o chi?n d?ch gây qu? m?i.</summary>
    [HttpPost]
    [Authorize(Roles = "1")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
    {
        var command = new CreateCampaignCommand(
            request.Name,
            request.Region,
            request.CampaignStartDate,
            request.CampaignEndDate,
            request.TargetAmount,
            GetUserId()
        );

        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    /// <summary>C?p nh?t thông tin co b?n (tên, khu v?c) c?a chi?n d?ch.</summary>
    [HttpPut("{id}/info")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInfo(int id, [FromBody] UpdateCampaignInfoRequest dto)
    {
        var command = new UpdateCampaignInfoCommand(
            id,
            dto.Name,
            dto.Region,
            GetUserId()
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Gia h?n ngày k?t thúc chi?n d?ch.</summary>
    [HttpPut("{id}/extension")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtendDuration(int id, [FromBody] ExtendCampaignRequest dto)
    {
        var command = new ExtendCampaignCommand(
            id,
            dto.NewEndDate,
            GetUserId()
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Tang m?c tiêu s? ti?n c?n gây qu?.</summary>
    [HttpPut("{id}/target")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IncreaseTarget(int id, [FromBody] IncreaseTargetRequest dto)
    {
        var command = new IncreaseTargetAmountCommand(
            id,
            dto.NewTarget,
            GetUserId()
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Thay d?i tr?ng thái chi?n d?ch (Active / Closed / ...).</summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusRequest dto)
    {
        var command = new ChangeCampaignStatusCommand(
            id,
            dto.NewStatus,
            GetUserId()
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Xóa m?m chi?n d?ch.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteCampaignCommand(id, GetUserId());
        await _mediator.Send(command);
        return NoContent();
    }

    // Helper to extract User ID from JWT Claims
    private Guid GetUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }
        // Fallback for development/testing if claims aren't populated correctly, 
        // or throw exception if strict.
        throw new UnauthorizedAccessException("Invalid User Token");
    }
}
