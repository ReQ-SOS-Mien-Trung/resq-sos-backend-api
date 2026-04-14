using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot-manager")]
    [ApiController]
    public class DepotManagerController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>L?y l?ch s? th? kho c?a m?t kho (depotId) có phân trang.</summary>
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

        /// <summary>
        /// G? manager dang active kh?i kho (soft-unassign): set UnassignedAt cho b?n ghi depot_managers,
        /// l?ch s? v?n du?c gi? l?i. Kho ph?i ? tr?ng thái Available.
        /// Sau khi g?, kho chuy?n v? PendingAssignment (ch? gán qu?n lý m?i).
        /// </summary>
        [HttpDelete("{depotId:int}")]
        [Authorize(Policy = PermissionConstants.PersonnelDepotBranchManage)]
        public async Task<IActionResult> Delete(int depotId)
        {
            var command = new UnassignDepotManagerCommand(depotId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
