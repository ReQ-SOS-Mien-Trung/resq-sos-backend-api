using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
using RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotById;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot")]
    [ApiController]
    public class DepotController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllDepotsQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetDepotByIdQuery(id));
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDepotRequestDto dto)
        {
            // Pass primitives to command. GeoLocation creation moved to Handler.
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                dto.Latitude,
                dto.Longitude,
                dto.Capacity
            );

            var result = await _mediator.Send(command);
            return StatusCode(201, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDepotRequestDto dto)
        {
            // Pass primitives to command.
            var command = new UpdateDepotCommand(
                id,
                dto.Name,
                dto.Address,
                dto.Latitude,
                dto.Longitude,
                dto.Capacity
            );

            await _mediator.Send(command);
            return NoContent();
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromQuery] ChangeDepotStatusRequestDto dto)
        {
            var command = new ChangeDepotStatusCommand(id, dto.Status);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpGet("metadata/depot-statuses")]
        public async Task<IActionResult> GetDepotStatuses()
        {
            var result = await _mediator.Send(new GetDepotStatusMetadataQuery());
            return Ok(result);
        }
    }
}
