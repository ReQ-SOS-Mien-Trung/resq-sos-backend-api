using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandHandler(
    IMissionActivityStatusExecutionService missionActivityStatusExecutionService,
    IUnitOfWork unitOfWork,
    ILogger<UpdateActivityStatusCommandHandler> logger
) : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
{
    private readonly IMissionActivityStatusExecutionService _missionActivityStatusExecutionService = missionActivityStatusExecutionService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateActivityStatusCommandHandler> _logger = logger;

    public async Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating status for MissionId={MissionId} ActivityId={ActivityId} -> {Status}",
            request.MissionId,
            request.ActivityId,
            request.Status);

        MissionActivityStatusExecutionResult? executionResult = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            executionResult = await _missionActivityStatusExecutionService.ApplyAsync(
                request.MissionId,
                request.ActivityId,
                request.Status,
                request.DecisionBy,
                request.ImageUrl,
                cancellationToken);
        });

        return new UpdateActivityStatusResponse
        {
            ActivityId = request.ActivityId,
            Status = executionResult!.EffectiveStatus.ToString(),
            DecisionBy = request.DecisionBy,
            ImageUrl = executionResult.ImageUrl,
            ConsumedItems = executionResult.ConsumedItems
        };
    }
}
