using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;
using RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

namespace RESQ.Presentation.Controllers.Personnel
{
    [Route("personnel/assembly-point")]
    [ApiController]
    public class AssemblyPointsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách điểm tập kết có phân trang.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllAssemblyPointsQuery { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách trạng thái điểm tập kết dùng cho dropdown.</summary>
        [HttpGet("status-metadata")]
        public async Task<IActionResult> GetStatusMetadata()
        {
            var query = new GetAssemblyPointStatusMetadataQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Xem chi tiết điểm tập kết theo ID.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var query = new GetAssemblyPointByIdQuery(id);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Tạo điểm tập kết mới.</summary>
        [HttpPost]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Create([FromBody] CreateAssemblyPointRequestDto dto)
        {
            var command = new CreateAssemblyPointCommand(
                dto.Name,
                dto.Latitude,
                dto.Longitude,
                dto.CapacityTeams
            );

            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>Cập nhật điểm tập kết (Code không thay đổi).</summary>
        [HttpPut("{id}")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAssemblyPointRequestDto dto)
        {
            var command = new UpdateAssemblyPointCommand(
                id,
                dto.Name,
                dto.Latitude,
                dto.Longitude,
                dto.CapacityTeams
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Thay đổi trạng thái điểm tập kết.</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeAssemblyPointStatusRequestDto dto)
        {
            var command = new ChangeAssemblyPointStatusCommand(id, dto.Status);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xóa điểm tập kết.</summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Delete(int id)
        {
            var command = new DeleteAssemblyPointCommand(id);
            await _mediator.Send(command);
            return NoContent();
        }
    }
}
