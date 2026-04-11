using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Enum.Operations;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

public class UpdateActivityStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExecutesSharedServiceInsideTransactionAndMapsResponse()
    {
        var executionService = new StubMissionActivityStatusExecutionService
        {
            NextResult = new MissionActivityStatusExecutionResult
            {
                EffectiveStatus = MissionActivityStatus.PendingConfirmation,
                CurrentServerStatus = MissionActivityStatus.PendingConfirmation,
                ConsumedItems =
                [
                    new SupplyExecutionItemDto
                    {
                        ItemModelId = 7,
                        ItemName = "Water",
                        Quantity = 4,
                        Unit = "bottle"
                    }
                ]
            }
        };
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateActivityStatusCommandHandler(
            executionService,
            unitOfWork,
            NullLogger<UpdateActivityStatusCommandHandler>.Instance);

        var decisionBy = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

        var response = await handler.Handle(
            new UpdateActivityStatusCommand(12, 34, MissionActivityStatus.Succeed, decisionBy),
            CancellationToken.None);

        Assert.Equal(1, unitOfWork.ExecuteInTransactionCalls);
        Assert.Single(executionService.Calls);

        var call = executionService.Calls[0];
        Assert.Equal(12, call.ExpectedMissionId);
        Assert.Equal(34, call.ActivityId);
        Assert.Equal(MissionActivityStatus.Succeed, call.RequestedStatus);
        Assert.Equal(decisionBy, call.DecisionBy);

        Assert.Equal(34, response.ActivityId);
        Assert.Equal(MissionActivityStatus.PendingConfirmation.ToString(), response.Status);
        Assert.Equal(decisionBy, response.DecisionBy);
        Assert.Single(response.ConsumedItems);
        Assert.Equal(7, response.ConsumedItems[0].ItemModelId);
    }

    private sealed class StubMissionActivityStatusExecutionService : IMissionActivityStatusExecutionService
    {
        public List<(int ExpectedMissionId, int ActivityId, MissionActivityStatus RequestedStatus, Guid DecisionBy)> Calls { get; } = [];

        public MissionActivityStatusExecutionResult NextResult { get; set; } = new()
        {
            EffectiveStatus = MissionActivityStatus.Succeed,
            CurrentServerStatus = MissionActivityStatus.Succeed
        };

        public Task<MissionActivityStatusExecutionResult> ApplyAsync(
            int expectedMissionId,
            int activityId,
            MissionActivityStatus requestedStatus,
            Guid decisionBy,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((expectedMissionId, activityId, requestedStatus, decisionBy));
            return Task.FromResult(NextResult);
        }
    }
}