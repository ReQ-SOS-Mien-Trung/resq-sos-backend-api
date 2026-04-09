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

        /// <summary>Lấy lịch sử thủ kho của một kho (depotId) có phân trang.</summary>
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
        /// Gỡ manager đang active khỏi kho (soft-unassign): set UnassignedAt cho bản ghi depot_managers,
        /// lịch sử vẫn được giữ lại. Kho phải ở trạng thái Available, Full hoặc UnderMaintenance.
        /// Sau khi gỡ, kho chuyển về PendingAssignment (chờ gán quản lý mới).
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