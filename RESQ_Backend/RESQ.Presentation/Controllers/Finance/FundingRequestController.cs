using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;
using RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;
using RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;
using RESQ.Application.UseCases.Finance.Queries.GetFundSourceTypesMetadata;
using RESQ.Application.UseCases.Finance.Queries.GenerateFundingRequestTemplate;
using RESQ.Application.UseCases.Finance.Queries.GetFundingRequestItems;
using RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;
using RESQ.Domain.Enum.Finance;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/funding-requests")]
[ApiController]
public class FundingRequestController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Cách 2] Manager kho g?i yêu c?u c?p thêm qu? kèm danh sách v?t ph?m. DepotId du?c t? d?ng l?y t? token.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateFundingRequestRequest request)
    {
        var command = new CreateFundingRequestCommand(
            request.Description,
            request.Items.Select(i => new FundingRequestItemDto
            {
                Row          = i.Row,
                ItemName     = i.ItemName,
                CategoryCode = i.CategoryCode,
                TargetGroup  = i.TargetGroup,
                ItemType     = i.ItemType,
                Unit         = i.Unit,
                Description  = i.Description,
                ImageUrl     = i.ImageUrl,
                Quantity     = i.Quantity,
                UnitPrice    = i.UnitPrice,
                VolumePerUnit = i.VolumePerUnit,
                WeightPerUnit = i.WeightPerUnit
            }).ToList(),
            GetUserId()
        );

        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    /// <summary>Admin duy?t yêu c?u - ch?n ngu?n qu? (Campaign ho?c SystemFund).</summary>
    [HttpPatch("{id}/approve")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveFundingRequestRequest request)
    {
        var command = new ApproveFundingRequestCommand(id, request.SourceType, request.CampaignId, GetUserId());
        var disbursementId = await _mediator.Send(command);
        return Ok(new { DisbursementId = disbursementId });
    }

    /// <summary>Admin t? ch?i yêu c?u c?p qu?.</summary>
    [HttpPatch("{id}/reject")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectFundingRequestRequest request)
    {
        var command = new RejectFundingRequestCommand(id, request.Reason, GetUserId());
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Tr? v? danh sách các giá tr? enum FundingRequestStatus.</summary>
    [HttpGet("metadata/statuses")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetStatuses()
    {
        var values = Enum.GetNames<FundingRequestStatus>();
        return Ok(values);
    }

    /// <summary>[Metadata] Danh sách lo?i ngu?n qu? dùng cho dropdown duy?t yêu c?u c?p qu?.</summary>
    [HttpGet("metadata/source-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFundSourceTypeMetadata()
    {
        var result = await _mediator.Send(new GetFundSourceTypesMetadataQuery());
        return Ok(result);
    }

    /// <summary>Tai file Excel mau yeu cau cap quy gom 11 cot, co them volumePerUnit va weightPerUnit.</summary>
    [HttpGet("template")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate()
    {
        var result = await _mediator.Send(new GenerateFundingRequestTemplateQuery());
        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>L?y danh sách yêu c?u c?p qu? (filter theo nhi?u depot, nhi?u status).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    [ProducesResponseType(typeof(PagedResult<FundingRequestListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] List<int>? depotIds = null,
        [FromQuery] List<FundingRequestStatus>? statuses = null)
    {
        var query = new GetFundingRequestsQuery(pageNumber, pageSize, depotIds, statuses);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>L?y danh sách dòng v?t ph?m trong m?t yêu c?u c?p qu? (phân trang).</summary>
    [HttpGet("{id}/items")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    [ProducesResponseType(typeof(PagedResult<FundingRequestItemListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        int id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetFundingRequestItemsQuery(id, pageNumber, pageSize);
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
