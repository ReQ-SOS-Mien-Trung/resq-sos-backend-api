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
        /// Gỡ manager khỏi kho (soft-unassign). Lịch sử vẫn được giữ lại.<br/>
        /// - Không truyền body (hoặc UserIds rỗng): gỡ TẤT CẢ manager đang active, kho về PendingAssignment.<br/>
        /// - Truyền UserIds: chỉ gỡ những người được liệt kê, kho giữ Available nếu còn manager khác.
        /// </summary>
        [HttpDelete("{depotId:int}")]
        [Authorize(Policy = PermissionConstants.PersonnelDepotBranchManage)]
        public async Task<IActionResult> Delete(int depotId, [FromBody] UnassignDepotManagerRequestDto? dto = null)
        {
            var userIds = dto?.UserIds is { Count: > 0 } list ? list.AsReadOnly() : null;
            var command = new UnassignDepotManagerCommand(depotId, GetUserId(), userIds);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdString, out var userId)) return userId;
            throw new UnauthorizedAccessException("Token không hợp lệ hoặc thiếu thông tin người dùng.");
        }
    }
}
