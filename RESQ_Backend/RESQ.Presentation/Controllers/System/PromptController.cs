using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.SystemConfig.Commands.ActivatePromptVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.CreatePromptDraft;
using RESQ.Application.UseCases.SystemConfig.Commands.DeletePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;
using RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAllPrompts;
using RESQ.Application.UseCases.SystemConfig.Queries.GetPromptById;
using RESQ.Application.UseCases.SystemConfig.Queries.GetPromptVersions;
using RESQ.Application.UseCases.SystemConfig.Queries.PromptMetadata;

namespace RESQ.Presentation.Controllers.System
{
    [Route("system/prompts")]
    [ApiController]
    public class PromptController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách prompt AI có phân trang.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllPromptsQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Xem chi tiết prompt theo ID.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetPromptByIdQuery(id));
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sach loai prompt.</summary>
        [HttpGet("metadata/prompt-types")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPromptTypesMetadata()
        {
            var result = await _mediator.Send(new GetPromptTypesMetadataQuery());
            return Ok(result);
        }

        /// <summary>Lay tat ca version theo prompt type cua prompt duoc chon.</summary>
        [HttpGet("{id:int}/versions")]
        public async Task<IActionResult> GetVersions(int id)
        {
            var result = await _mediator.Send(new GetPromptVersionsQuery(id));
            return Ok(result);
        }

        /// <summary>Tạo prompt AI mới.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePromptRequestDto dto)
        {
            var command = new CreatePromptCommand(
                dto.Name,
                dto.PromptType,
                dto.Provider,
                dto.Purpose,
                dto.SystemPrompt,
                dto.UserPromptTemplate,
                dto.Model,
                dto.Temperature,
                dto.MaxTokens,
                dto.Version,
                dto.ApiUrl,
                dto.ApiKey,
                dto.IsActive
            );

            var result = await _mediator.Send(command);
            return StatusCode(201, result);
        }

        /// <summary>Clone mot prompt version thanh draft moi.</summary>
        [HttpPost("{id:int}/drafts")]
        public async Task<IActionResult> CreateDraft(int id)
        {
            var result = await _mediator.Send(new CreatePromptDraftCommand(id));
            return Ok(result);
        }

        /// <summary>Cap nhat draft prompt AI (chi gui truong can thay doi).</summary>
        [HttpPut("drafts/{id:int}")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePromptRequestDto dto)
        {
            var command = new UpdatePromptCommand(
                id,
                dto.Name,
                dto.PromptType,
                dto.Provider,
                dto.Purpose,
                dto.SystemPrompt,
                dto.UserPromptTemplate,
                dto.Model,
                dto.Temperature,
                dto.MaxTokens,
                dto.Version,
                dto.ApiUrl,
                dto.ApiKey,
                dto.IsActive
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Xóa prompt AI theo ID.</summary>
        [HttpDelete("drafts/{id:int}")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeletePromptCommand(id));
            return NoContent();
        }

        /// <summary>Activate mot prompt version.</summary>
        [HttpPost("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var result = await _mediator.Send(new ActivatePromptVersionCommand(id));
            return Ok(result);
        }

        /// <summary>Rollback ve mot version prompt archived.</summary>
        [HttpPost("{id:int}/rollback")]
        public async Task<IActionResult> Rollback(int id)
        {
            var result = await _mediator.Send(new RollbackPromptVersionCommand(id));
            return Ok(result);
        }

        /// <summary>Preview ke hoach mission AI theo draft prompt moi chua luu.</summary>
        [HttpPost("test")]
        public async Task<IActionResult> TestNewModel([FromBody] TestNewPromptRequestDto? dto)
        {
            dto ??= new TestNewPromptRequestDto();

            var result = await _mediator.Send(new TestPromptCommand(
                null,
                TestPromptDraftMode.NewPromptDraft,
                dto.ClusterId,
                dto.Name,
                dto.PromptType,
                dto.Provider,
                dto.Purpose,
                dto.SystemPrompt,
                dto.UserPromptTemplate,
                dto.Model,
                dto.Temperature,
                dto.MaxTokens,
                dto.Version,
                dto.ApiUrl,
                dto.ApiKey,
                dto.IsActive));

            return Ok(result);
        }

        /// <summary>Preview ke hoach mission AI theo draft cap nhat prompt va cum SOS.</summary>
        [HttpPost("{id}/test")]
        public async Task<IActionResult> TestModel(int id, [FromBody] TestPromptRequestDto? dto)
        {
            dto ??= new TestPromptRequestDto();

            var result = await _mediator.Send(new TestPromptCommand(
                id,
                TestPromptDraftMode.ExistingPromptDraft,
                dto.ClusterId,
                dto.Name,
                dto.PromptType,
                dto.Provider,
                dto.Purpose,
                dto.SystemPrompt,
                dto.UserPromptTemplate,
                dto.Model,
                dto.Temperature,
                dto.MaxTokens,
                dto.Version,
                dto.ApiUrl,
                dto.ApiKey,
                dto.IsActive));

            return Ok(result);
        }
    }
}
