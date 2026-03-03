using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandHandler(
    IMissionActivityRepository activityRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateActivityStatusCommandHandler> logger
) : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateActivityStatusCommandHandler> _logger = logger;

    public async Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for ActivityId={activityId} -> {status}", request.ActivityId, request.Status);

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
            throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        await _activityRepository.UpdateStatusAsync(request.ActivityId, request.Status, request.DecisionBy, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateActivityStatusResponse
        {
            ActivityId = request.ActivityId,
            Status = request.Status.ToString(),
            DecisionBy = request.DecisionBy
        };
    }
}
