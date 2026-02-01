using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("api/depot")]
    [ApiController]
    public class DepotController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var depots = await _mediator.Send(new GetAllDepotsQuery());
            return Ok(depots);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateDepotRequestDto dto)
        {
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                new GeoLocation(dto.Latitude, dto.Longitude),
                dto.Capacity
                );
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
