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
using RESQ.Application.UseCases.Finance.Queries.GetCampaignTransactions;
using RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;
using RESQ.Application.UseCases.Finance.Queries.ViewCampaignMetadata;
using RESQ.Domain.Enum.Finance;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/campaigns")]
[ApiController]
public class FundCampaignController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy danh sách chiến dịch gây quỹ có phân trang. Filter theo status (có thể truyền nhiều giá trị).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CampaignListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] List<FundCampaignStatus>? statuses = null)
    {
        var query = new ViewAllCampaignsQuery(pageNumber, pageSize, statuses);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách chiến dịch đang hoạt động dùng cho dropdown.</summary>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata()
    {
        var query = new ViewCampaignMetadataQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách tất cả trạng thái chiến dịch (key = int, value = tên).</summary>
    [HttpGet("metadata/statuses")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetStatusMetadata()
    {
        var result = Enum.GetValues<FundCampaignStatus>()
            .Select(s => new MetadataDto
            {
                Key = ((int)s).ToString(),
                Value = s.ToString()
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>Tạo chiến dịch gây quỹ mới.</summary>
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

    /// <summary>Cập nhật thông tin cơ bản (tên, khu vực) của chiến dịch.</summary>
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

    /// <summary>Gia hạn ngày kết thúc chiến dịch.</summary>
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

    /// <summary>Tăng mục tiêu số tiền cần gây quỹ.</summary>
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

    /// <summary>Thay đổi trạng thái chiến dịch (Active / Closed / ...).</summary>
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
            GetUserId(),
            dto.Reason
        );

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Xóa mềm chiến dịch.</summary>
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

    /// <summary>Lấy lịch sử giao dịch tài chính của chiến dịch (bắt buộc theo campaign ID, có phân trang).</summary>
    [HttpGet("{id}/transactions")]
    [Authorize(Roles = "1")]
    [ProducesResponseType(typeof(PagedResult<FundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions(
        int id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetCampaignTransactionsQuery(id, pageNumber, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
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
