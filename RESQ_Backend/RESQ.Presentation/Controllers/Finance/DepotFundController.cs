using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;
using RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFundTransactions;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/depot-funds")]
[ApiController]
public class DepotFundController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Metadata] Danh sách loại giao dịch quỹ kho (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/transaction-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetTransactionTypeMetadata()
    {
        var result = Enum.GetValues<DepotFundTransactionType>()
            .Select(t => new MetadataDto
            {
                Key   = t.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.DepotFundTransactionTypeLabels, t.ToString())
            })
            .ToList();
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại tham chiếu giao dịch quỹ kho (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/reference-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetReferenceTypeMetadata()
    {
        var result = FinanceLabels.DepotFundReferenceTypeLabels
            .Select(kv => new MetadataDto { Key = kv.Key, Value = kv.Value })
            .ToList();
        return Ok(result);
    }

    /// <summary>[Admin] Xem số dư quỹ tất cả kho.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(List<DepotFundListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllDepotFundsQuery());
        return Ok(result);
    }

    /// <summary>[Manager] Xem số dư quỹ kho mình đang quản lý.</summary>
    [HttpGet("my")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(DepotFundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMy()
    {
        var result = await _mediator.Send(new GetMyDepotFundQuery(GetUserId()));
        return Ok(result);
    }

    /// <summary>[Admin] Lấy lịch sử giao dịch quỹ của một kho theo depot ID.</summary>
    [HttpGet("{depotId}/transactions")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(PagedResult<DepotFundTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionsByDepot(
        int depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetDepotFundTransactionsQuery(depotId, pageNumber, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Manager] Lấy lịch sử giao dịch quỹ kho mình đang quản lý.</summary>
    [HttpGet("my/transactions")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(PagedResult<DepotFundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetMyDepotFundTransactionsQuery(GetUserId(), pageNumber, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Admin] Cấu hình hạn mức tự ứng (balance âm tối đa) cho một kho.</summary>
    [HttpPut("{depotId:int}/advance-limit")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetAdvanceLimit(int depotId, [FromBody] SetAdvanceLimitRequest request)
    {
        await _mediator.Send(new SetDepotAdvanceLimitCommand(depotId, request.MaxAdvanceLimit));
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId))
            return userId;
        throw new UnauthorizedAccessException("Invalid User Token");
    }
}
