using System.Linq;
using RESQ.Infrastructure.Persistence.Seeding;

namespace RESQ.Tests.Infrastructure.Seeding;

public class PromptSeederTests
{
    [Fact]
    public void CreatePrompts_DoesNotSeedLegacyMissionPlanningPrompts()
    {
        var prompts = SystemSeeder.CreatePrompts();

        Assert.DoesNotContain(prompts, prompt => prompt.PromptType == "MissionPlanning");
    }

    [Fact]
    public void CreatePrompts_MissionRequirementsAssessmentV21_ContainsStrictJsonArrayRules()
    {
        var prompt = SystemSeeder.CreatePrompts().Single(item => item.Id == 10);

        Assert.Equal("MissionRequirementsAssessment", prompt.PromptType);
        Assert.Equal("v2.1", prompt.Version);
        Assert.Contains("IMPORTANT JSON RULES FOR suggested_resources (STRICT):", prompt.SystemPrompt);
        Assert.Contains("- suggested_resources MUST be an array of JSON objects only.", prompt.SystemPrompt);
        Assert.Contains("IMPORTANT JSON RULES FOR sos_requirements (STRICT):", prompt.SystemPrompt);
        Assert.Contains("- required_supplies MUST be an array of JSON objects only.", prompt.SystemPrompt);
        Assert.Contains("- required_teams MUST be an array of JSON objects only.", prompt.SystemPrompt);
    }

    [Theory]
    [InlineData(6, "v1.0")]
    [InlineData(11, "v2.1")]
    public void CreatePrompts_MissionTeamPlanningVersions_ContainOrderedRouteAndSuggestedTeamRules(
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

    [Fact]
    public void CreatePrompts_ActiveAiPrompts_DoNotRequestDeprecatedScoreField()
    {
        var prompts = SystemSeeder.CreatePrompts()
            .Where(item => item.IsActive)
            .ToArray();
        var deprecatedField = "confidence" + "_score";

        Assert.DoesNotContain(prompts, prompt => prompt.SystemPrompt?.Contains(deprecatedField) == true);
        Assert.DoesNotContain(prompts, prompt => prompt.UserPromptTemplate?.Contains(deprecatedField) == true);
    }
}
