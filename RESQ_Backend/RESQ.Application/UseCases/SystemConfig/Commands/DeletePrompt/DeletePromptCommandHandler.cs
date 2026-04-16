using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeletePrompt;

public class DeletePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeletePromptCommandHandler> logger) : IRequestHandler<DeletePromptCommand>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<DeletePromptCommandHandler> _logger = logger;

    public async Task Handle(DeletePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

            if (!PromptLifecycleStatusResolver.IsDraft(prompt))
        {
                throw new BadRequestException("Chi draft prompt moi co the xoa.");
        }

        await _promptRepository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Deleted prompt successfully: Id={Id}", request.Id);
    }
}
