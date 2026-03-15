using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot-manager")]
    [ApiController]
    public class DepotManagerController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpGet]
        [Authorize(Policy = PermissionConstants.PersonnelDepotBranchManage)]
        public async Task<IActionResult> Get([FromQuery] int depotId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetDepotManagerHistoryQuery(depotId)
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}