using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotById; // New Import
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("api/depot")]
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

        //// --- NEW GET BY ID METHOD ---
        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetById(int id)
        //{
        //    var result = await _mediator.Send(new GetDepotByIdQuery(id));
        //    return result != null ? Ok(result) : NotFound();
        //}

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDepotRequestDto dto)
        {
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                new GeoLocation(dto.Latitude, dto.Longitude),
                dto.Capacity
            );

            var result = await _mediator.Send(command);
            return StatusCode(201, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDepotRequestDto dto)
        {
            var command = new UpdateDepotCommand(
                id,
                dto.Name,
                dto.Address,
                new GeoLocation(dto.Latitude, dto.Longitude),
                dto.Capacity
            );

            await _mediator.Send(command);
            return NoContent();
        }
    }
}