using System.Linq;
using RESQ.Infrastructure.Persistence.Seeding;

namespace RESQ.Tests.Infrastructure.Seeding;

public class PromptSeederTests
{
    [Fact]
    public void CreatePrompts_MissionPlanningV2_IncludesExplicitJsonResponseFormat()
    {
        var prompt = SystemSeeder.CreatePrompts().Single(item => item.Id == 8);

        Assert.Equal("MissionPlanning", prompt.PromptType);
        Assert.Equal("v2.0", prompt.Version);
        Assert.Contains("FORMAT JSON PHẢN HỒI", prompt.SystemPrompt);
        Assert.Contains(@"""mission_title""", prompt.SystemPrompt);
        Assert.Contains(@"""activities""", prompt.SystemPrompt);
        Assert.Contains(@"""resources""", prompt.SystemPrompt);
        Assert.Contains(@"""supply_shortages""", prompt.SystemPrompt);
    }

    [Fact]
    public void CreatePrompts_MissionRequirementsAssessmentV2_ContainsStrictJsonArrayRules()
    {
        var prompt = SystemSeeder.CreatePrompts().Single(item => item.Id == 10);

        Assert.Equal("MissionRequirementsAssessment", prompt.PromptType);
        Assert.Equal("v2.0", prompt.Version);
        Assert.Contains("IMPORTANT JSON RULES FOR suggested_resources (STRICT):", prompt.SystemPrompt);
        Assert.Contains("- suggested_resources MUST be an array of JSON objects only.", prompt.SystemPrompt);
        Assert.Contains("IMPORTANT JSON RULES FOR sos_requirements (STRICT):", prompt.SystemPrompt);
        Assert.Contains("- required_supplies MUST be an array of JSON objects only.", prompt.SystemPrompt);
        Assert.Contains("- required_teams MUST be an array of JSON objects only.", prompt.SystemPrompt);
    }

    [Theory]
    [InlineData(6, "v1.0")]
    [InlineData(11, "v2.0")]
    public void CreatePrompts_MissionTeamPlanningVersions_ContainOrderedRouteAndStrictSuggestedTeamRules(
        int promptId,
        string version)
    {
        var prompt = SystemSeeder.CreatePrompts().Single(item => item.Id == promptId);

        Assert.Equal("MissionTeamPlanning", prompt.PromptType);
        Assert.Equal(version, prompt.Version);
        Assert.Contains(@"""ordered_activity_keys""", prompt.SystemPrompt);
        Assert.Contains("IMPORTANT JSON RULES FOR suggested_team (STRICT):", prompt.SystemPrompt);
        Assert.Contains("top-level `suggested_team = null` exactly", prompt.SystemPrompt);
        Assert.Contains("IMPORTANT JSON RULES FOR ordered_activity_keys (STRICT):", prompt.SystemPrompt);
        Assert.Contains("Phải chứa mọi `activity_key` từ `depot_fragment.activities`", prompt.SystemPrompt);
        Assert.Contains("Handoff inventory giữa teams không được backend hỗ trợ.", prompt.SystemPrompt);
        Assert.Contains("Mọi `DELIVER_SUPPLIES` phải nằm cùng route/team với `COLLECT_SUPPLIES`", prompt.SystemPrompt);
    }

    [Fact]
    public void CreatePrompts_PipelineDefaults_ActivateDepotPromptAndOnlyLatestTeamPrompt()
    {
        var prompts = SystemSeeder.CreatePrompts();

        var activeDepotPromptIds = prompts
            .Where(item => item.PromptType == "MissionDepotPlanning" && item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        var activeTeamPromptIds = prompts
            .Where(item => item.PromptType == "MissionTeamPlanning" && item.IsActive)
            .Select(item => item.Id)
            .ToArray();

        Assert.Equal([5], activeDepotPromptIds);
        Assert.Equal([11], activeTeamPromptIds);
    }
}
