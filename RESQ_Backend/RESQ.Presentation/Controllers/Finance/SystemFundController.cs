using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetFundSourceTypesMetadata;
using RESQ.Application.UseCases.Finance.Queries.GetSystemFund;
using RESQ.Application.UseCases.Finance.Queries.GetSystemFundTransactions;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/system-fund")]
[ApiController]
public class SystemFundController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Metadata] Danh sách loại nguồn quỹ (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/source-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSourceTypeMetadata()
    {
        var result = await _mediator.Send(new GetFundSourceTypesMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại giao dịch quỹ hệ thống (key = tên tiếng Anh, value = tên tiếng Việt).</summary>
    [HttpGet("metadata/transaction-types")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public IActionResult GetTransactionTypeMetadata()
    {
        var result = Enum.GetValues<SystemFundTransactionType>()
            .Select(t => new MetadataDto
            {
                Key   = t.ToString(),
                Value = FinanceLabels.Translate(FinanceLabels.SystemFundTransactionTypeLabels, t.ToString())
            })
            .ToList();
        return Ok(result);
    }

    /// <summary>[Admin] Xem thông tin quỹ hệ thống (số dư, tên, ngày cập nhật cuối).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(SystemFundDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetSystemFundQuery());
        return Ok(result);
    }

    /// <summary>[Admin] Lấy lịch sử giao dịch quỹ hệ thống (phân trang).</summary>
    [HttpGet("transactions")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(PagedResult<SystemFundTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetSystemFundTransactionsQuery(pageNumber, pageSize));
        return Ok(result);
    }
}
