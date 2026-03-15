using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.DeletePrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAllPrompts;
using RESQ.Application.UseCases.SystemConfig.Queries.GetPromptById;

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

        /// <summary>Tạo prompt AI mới.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePromptRequestDto dto)
        {
            var command = new CreatePromptCommand(
                dto.Name,
                dto.PromptType,
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

        /// <summary>Cập nhật prompt AI (chỉ gửi trường cần thay đổi).</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePromptRequestDto dto)
        {
            var command = new UpdatePromptCommand(
                id,
                dto.Name,
                dto.PromptType,
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeletePromptCommand(id));
            return NoContent();
        }

        /// <summary>Kiểm tra kết nối AI model theo cấu hình prompt.</summary>
        [HttpPost("{id}/test")]
        public async Task<IActionResult> TestModel(int id)
        {
            var result = await _mediator.Send(new TestPromptCommand(id));
            return Ok(result);
        }
    }
}
