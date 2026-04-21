using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Tests.Infrastructure.Mappers;

public class MissionAiSuggestionMapperTests
{
    [Fact]
    public void ToEntity_MapsSuggestedMissionTypeAndSeverity()
    {
        var model = new MissionAiSuggestionModel
        {
            SuggestedMissionTitle = "Mission",
            SuggestedMissionType = "MIXED",
            SuggestedSeverityLevel = "Critical"
        };

        var entity = MissionAiSuggestionMapper.ToEntity(model);

        Assert.Equal("MIXED", entity.SuggestedMissionType);
        Assert.Equal("Critical", entity.SuggestedSeverityLevel);
    }

    [Fact]
    public void ToDomain_MapsSuggestedMissionTypeAndSeverity()
    {
        var entity = new MissionAiSuggestion
        {
            SuggestedMissionTitle = "Mission",
            SuggestedMissionType = "RESCUE",
            SuggestedSeverityLevel = "Severe"
        };

        var model = MissionAiSuggestionMapper.ToDomain(entity);

        Assert.Equal("RESCUE", model.SuggestedMissionType);
        Assert.Equal("Severe", model.SuggestedSeverityLevel);
    }
}
