using MediatR;
using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// Get a paginated list of assembly points.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllAssemblyPointsQuery { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Get metadata for assembly point statuses (for UI dropdowns).
        /// </summary>
        [HttpGet("status-metadata")]
        public async Task<IActionResult> GetStatusMetadata()
        {
            var query = new GetAssemblyPointStatusMetadataQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Get a specific assembly point by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var query = new GetAssemblyPointByIdQuery(id);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Create a new assembly point.
        /// </summary>
        [HttpPost]
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

        /// <summary>
        /// Update an existing assembly point (Code is immutable).
        /// </summary>
        [HttpPut("{id}")]
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

        /// <summary>
        /// Change the status of an assembly point.
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromQuery] ChangeAssemblyPointStatusRequestDto dto)
        {
            var command = new ChangeAssemblyPointStatusCommand(id, dto.Status);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Delete an assembly point.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var command = new DeleteAssemblyPointCommand(id);
            await _mediator.Send(command);
            return NoContent();
        }
    }
}
