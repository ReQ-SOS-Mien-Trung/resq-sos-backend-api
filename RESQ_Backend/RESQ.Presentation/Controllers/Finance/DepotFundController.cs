using RESQ.Application.UseCases.Finance.Queries.GetDepotAdvancers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;
using RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;
using RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundTransactions;
using RESQ.Application.UseCases.Finance.Queries.GetDepotFundsByDepotId;
using RESQ.Application.UseCases.Finance.Queries.GetFundTransactionsByFundId;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotAdvanceTransactions;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFundTransactions;
using RESQ.Domain.Enum.Finance;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/depot-funds")]
[ApiController]
public class DepotFundController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet("metadata/transaction-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetTransactionTypeMetadata()
    {
        var result = Enum.GetValues<DepotFundTransactionType>()
            .Select(t => new MetadataDto
            {
                Key = t.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.DepotFundTransactionTypeLabels, t.ToString())
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("metadata/reference-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetReferenceTypeMetadata()
    {
        var result = FinanceLabels.DepotFundReferenceTypeLabels
            .Select(kv => new MetadataDto { Key = kv.Key, Value = kv.Value })
            .ToList();

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(PagedResult<DepotFundsResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAllDepotFundsQuery(pageNumber, pageSize, search));
        return Ok(result);
    }

    [HttpGet("{depotId:int}/funds")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(DepotFundsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFundsByDepot(int depotId)
    {
        var result = await _mediator.Send(new GetDepotFundsByDepotIdQuery(depotId));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("my/funds-metadata")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyFundsMetadata([FromQuery] int depotId)
    {
        var response = await _mediator.Send(new GetMyDepotFundQuery(GetUserId(), depotId));
        var result = response.Funds.Select(f => new MetadataDto
        {
            Key = f.Id.ToString(),
            Value = $"{f.FundSourceName ?? "Quỹ kho"} - {f.Balance:N0} VND"
        }).ToList();
        return Ok(result);
    }

    [HttpGet("{fundId:int}/fund-transactions")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(PagedResult<DepotFundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFundTransactions(
        int fundId,
        [FromQuery] int depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetFundTransactionsByFundIdQuery(fundId, pageNumber, pageSize, GetUserId(), depotId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(typeof(MyDepotFundsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMy([FromQuery] int depotId)
    {
        var result = await _mediator.Send(new GetMyDepotFundQuery(GetUserId(), depotId));
        return Ok(result);
    }

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

    [HttpGet("my/transactions")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(typeof(PagedResult<DepotFundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] int depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetMyDepotFundTransactionsQuery(GetUserId(), pageNumber, pageSize, depotId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("my/advance-transactions")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(typeof(PagedResult<DepotFundTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAdvanceTransactions(
        [FromQuery] int depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetMyDepotAdvanceTransactionsQuery(GetUserId(), pageNumber, pageSize, depotId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPut("{depotId:int}/advance-limit")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetAdvanceLimit(int depotId, [FromBody] SetAdvanceLimitRequest request)
    {
        await _mediator.Send(new SetDepotAdvanceLimitCommand(depotId, request.AdvanceLimit));
        return NoContent();
    }

    [HttpPost("{depotFundId:int}/advance")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Advance(int depotFundId, [FromBody] List<CreateAdvanceTransactionItemRequest> request)
    {
        var transactions = request
            .Select(x => new CreateAdvanceTransactionItem(x.Amount, x.ContributorName, x.PhoneNumber))
            .ToList();

        var command = new CreateAdvanceTransactionCommand(
            depotFundId,
            transactions,
            GetUserId());

        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPost("repayment")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Repay([FromBody] CreateRepaymentTransactionRequest request)
    {
        var command = new CreateRepaymentTransactionCommand(
            request.ContributorName,
            request.PhoneNumber,
            request.Repayments
                .Select(r => new RepaymentFundAllocation(r.DepotFundId, r.Amount))
                .ToList(),
            GetUserId());

        await _mediator.Send(command);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Token người dùng không hợp lệ.");
    }

    [HttpGet("my/advancers")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    public async Task<IActionResult> GetDepotAdvancers(
        [FromQuery] int depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = new GetDepotAdvancersQuery(userId, pageNumber, pageSize, depotId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Chart 3] Biểu đồ biến động quỹ kho (tiền vào/tiền ra theo ngày) – point styling line chart.
    /// </summary>
    [HttpGet("{depotId}/chart/fund-movement")]
    [Authorize(Policy = PermissionConstants.PolicyDepotView)]
    [ProducesResponseType(typeof(RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart.DepotFundMovementChartDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFundMovementChart(
        int depotId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new RESQ.Application.UseCases.Finance.Queries.GetDepotFundMovementChart.GetDepotFundMovementChartQuery
            {
                DepotId = depotId,
                From    = from,
                To      = to
            }, cancellationToken);
        return Ok(result);
    }
}
