using RESQ.Application.UseCases.SystemConfig.Queries.PromptMetadata;
using RESQ.Domain.Enum.System;

namespace RESQ.Tests.Application.UseCases.SystemConfig.Queries;

public class GetPromptTypesMetadataQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllPromptTypesInEnumOrderWithVietnameseLabels()
    {
        var handler = new GetPromptTypesMetadataQueryHandler();

        var result = await handler.Handle(new GetPromptTypesMetadataQuery(), CancellationToken.None);

        var expectedOrder = Enum.GetValues<PromptType>().Select(promptType => promptType.ToString()).ToList();
        var actualOrder = result.Select(item => item.Key).ToList();

        Assert.Equal(expectedOrder, actualOrder);
        Assert.Collection(result,
            item =>
            {
                Assert.Equal(nameof(PromptType.SosPriorityAnalysis), item.Key);
                Assert.Equal("Phân tích ưu tiên SOS", item.Value);
            },
            item =>
            {
                Assert.Equal(nameof(PromptType.MissionPlanning), item.Key);
                Assert.Equal("Lập kế hoạch nhiệm vụ", item.Value);
            },
            item =>
            {
                Assert.Equal(nameof(PromptType.MissionRequirementsAssessment), item.Key);
                Assert.Equal("Đánh giá nhu cầu nhiệm vụ", item.Value);
            },
            item =>
            {
                Assert.Equal(nameof(PromptType.MissionDepotPlanning), item.Key);
                Assert.Equal("Lập kế hoạch kho", item.Value);
            },
            item =>
            {
                Assert.Equal(nameof(PromptType.MissionTeamPlanning), item.Key);
                Assert.Equal("Lập kế hoạch đội cứu hộ", item.Value);
            },
            item =>
            {
                Assert.Equal(nameof(PromptType.MissionPlanValidation), item.Key);
                Assert.Equal("Kiểm tra kế hoạch nhiệm vụ", item.Value);
            });
    }
}
