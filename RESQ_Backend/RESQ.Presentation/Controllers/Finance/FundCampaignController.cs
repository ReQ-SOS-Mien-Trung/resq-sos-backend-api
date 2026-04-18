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
using RESQ.Application.Common.Constants;
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

    /// <summary>[Metadata] Danh sách tất cả trạng thái chiến dịch (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/statuses")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetStatusMetadata()
    {
        var result = Enum.GetValues<FundCampaignStatus>()
            .Select(s => new MetadataDto
            {
                Key   = s.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.FundCampaignStatusLabels, s.ToString())
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại giao dịch quỹ chiến dịch (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/transaction-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetTransactionTypeMetadata()
    {
        var result = Enum.GetValues<TransactionType>()
            .Select(t => new MetadataDto
            {
                Key   = t.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.TransactionTypeLabels, t.ToString())
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại tham chiếu giao dịch quỹ chiến dịch (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/reference-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetReferenceTypeMetadata()
    {
        var result = Enum.GetValues<TransactionReferenceType>()
            .Select(r => new MetadataDto
            {
                Key   = r.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.TransactionReferenceTypeLabels, r.ToString())
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách chiều giao dịch quỹ chiến dịch (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/directions")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetDirectionMetadata()
    {
        var result = Enum.GetValues<TransactionDirection>()
            .Select(d => new MetadataDto
            {
                Key   = d.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.DirectionLabels, d.ToString())
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>Tạo chiến dịch gây quỹ mới.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
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
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
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
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
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
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
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
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
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
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteCampaignCommand(id, GetUserId());
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Lấy lịch sử giao dịch tài chính của chiến dịch (bắt buộc theo campaign ID, có phân trang). Filter tùy chọn: types, directions, referenceTypes.</summary>
    [HttpGet("{id}/transactions")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(PagedResult<FundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions(
        int id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] List<TransactionType>?          types          = null,
        [FromQuery] List<TransactionDirection>?     directions     = null,
        [FromQuery] List<TransactionReferenceType>? referenceTypes = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate   = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null)
    {
        var query = new GetCampaignTransactionsQuery(id, pageNumber, pageSize, types, directions, referenceTypes, fromDate, toDate, minAmount, maxAmount);
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

    /// <summary>
    /// [Chart 4] Biểu đồ biến động quỹ chiến dịch (tiền vào / tiền ra / số dư thuần theo kỳ) – bar chart 3 cột.
    /// Granularity: "month" (mặc định) hoặc "week".
    /// </summary>
    [HttpGet("{id}/chart/fund-flow")]
    [ProducesResponseType(typeof(RESQ.Application.UseCases.Finance.Queries.GetCampaignFundFlowChart.CampaignFundFlowChartDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCampaignFundFlowChart(
        int id,
        [FromQuery] DateTime? from         = null,
        [FromQuery] DateTime? to           = null,
        [FromQuery] string    granularity  = "month",
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new RESQ.Application.UseCases.Finance.Queries.GetCampaignFundFlowChart.GetCampaignFundFlowChartQuery
            {
                CampaignId  = id,
                From        = from,
                To          = to,
                Granularity = granularity
            }, cancellationToken);
        return Ok(result);
    }
}
