using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.SystemConfig.Commands.ActivateAiConfigVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfigDraft;
using RESQ.Application.UseCases.SystemConfig.Commands.DeleteAiConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.RollbackAiConfigVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigById;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigVersions;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAllAiConfigs;

namespace RESQ.Presentation.Controllers.System;

[Route("system/ai-configs")]
[ApiController]
public class AiConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = new GetAllAiConfigsQuery
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
        var result = await _mediator.Send(new GetAiConfigByIdQuery(id));
        return Ok(result);
    }

    [HttpGet("{id:int}/versions")]
    public async Task<IActionResult> GetVersions(int id)
    {
        var result = await _mediator.Send(new GetAiConfigVersionsQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAiConfigRequestDto dto)
    {
        var command = new CreateAiConfigCommand(
            dto.Name,
            dto.Provider,
            dto.Model,
            dto.Temperature,
            dto.MaxTokens,
            dto.ApiUrl,
            dto.ApiKey,
            dto.Version,
            dto.IsActive);

        var result = await _mediator.Send(command);
        return StatusCode(201, result);
    }

    [HttpPost("{id:int}/drafts")]
    public async Task<IActionResult> CreateDraft(int id)
    {
        var result = await _mediator.Send(new CreateAiConfigDraftCommand(id));
        return Ok(result);
    }

    [HttpPut("drafts/{id:int}")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAiConfigRequestDto dto)
    {
        var command = new UpdateAiConfigCommand(
            id,
            dto.Name,
            dto.Provider,
            dto.Model,
            dto.Temperature,
            dto.MaxTokens,
            dto.ApiUrl,
            dto.ApiKey,
            dto.Version,
            dto.IsActive);

        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("drafts/{id:int}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteAiConfigCommand(id));
        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var result = await _mediator.Send(new ActivateAiConfigVersionCommand(id));
        return Ok(result);
    }

    [HttpPost("{id:int}/rollback")]
    public async Task<IActionResult> Rollback(int id)
    {
        var result = await _mediator.Send(new RollbackAiConfigVersionCommand(id));
        return Ok(result);
    }
}
