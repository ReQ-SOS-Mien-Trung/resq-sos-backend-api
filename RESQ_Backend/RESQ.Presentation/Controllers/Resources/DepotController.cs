using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Resources.Commands.CreateDepot;
using RESQ.Application.UseCases.Resources.Queries.GetAllDepots;
using RESQ.Domain.Entities.Resources.ValueObjects;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Resources
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
            var userId = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                new GeoLocation(dto.Latitude, dto.Longitude),
                dto.Capacity,
                Guid.TryParse(userId, out var guid) ? guid : null
                );
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
