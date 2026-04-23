using RESQ.Application.UseCases.SystemConfig.Queries.PromptMetadata;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Queries;

public class GetPromptTypesMetadataQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPipelinePromptTypesWithoutLegacyMissionPlanning()
    {
        var handler = new GetPromptTypesMetadataQueryHandler();

        var result = await handler.Handle(new GetPromptTypesMetadataQuery(), CancellationToken.None);

        var expectedOrder = Enum.GetValues<PromptType>()
            .Where(promptType => promptType != PromptType.MissionPlanning)
            .Select(promptType => promptType.ToString())
            .ToList();
        var actualOrder = result.Select(item => item.Key).ToList();

        Assert.Equal(expectedOrder, actualOrder);
        Assert.Contains(result, item => item.Key == nameof(PromptType.SosPriorityAnalysis));
        Assert.Contains(result, item => item.Key == nameof(PromptType.MissionRequirementsAssessment));
        Assert.Contains(result, item => item.Key == nameof(PromptType.MissionDepotPlanning));
        Assert.Contains(result, item => item.Key == nameof(PromptType.MissionTeamPlanning));
        Assert.Contains(result, item => item.Key == nameof(PromptType.MissionPlanValidation));
        Assert.DoesNotContain(result, item => item.Key == nameof(PromptType.MissionPlanning));
    }
}
