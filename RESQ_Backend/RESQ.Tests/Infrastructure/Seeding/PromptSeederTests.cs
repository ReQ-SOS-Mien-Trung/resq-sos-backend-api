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
    public void CreatePrompts_ActiveMissionSuggestionPrompts_ContainSosCoverageContract()
    {
        var prompts = SystemSeeder.CreatePrompts()
            .Where(item => item.IsActive)
            .ToArray();

        var requirements = prompts.Single(item => item.PromptType == "MissionRequirementsAssessment");
        var depot = prompts.Single(item => item.PromptType == "MissionDepotPlanning");
        var team = prompts.Single(item => item.PromptType == "MissionTeamPlanning");
        var validation = prompts.Single(item => item.PromptType == "MissionPlanValidation");

        Assert.Contains("IMPORTANT SOS COVERAGE CONTRACT (STRICT):", requirements.SystemPrompt);
        Assert.Contains("exact sos_request_id", requirements.SystemPrompt);

        Assert.Contains("IMPORTANT SOS COVERAGE CONTRACT (STRICT):", depot.SystemPrompt);
        Assert.Contains("IMPORTANT ITEM TYPE CONTRACT (STRICT):", depot.SystemPrompt);
        Assert.Contains("Never put Reusable items in DELIVER_SUPPLIES", depot.SystemPrompt);
        Assert.Contains("DELIVER_SUPPLIES", depot.SystemPrompt);
        Assert.Contains("supply_shortages", depot.SystemPrompt);
        Assert.Contains("exact sos_request_id", depot.SystemPrompt);

        Assert.Contains("IMPORTANT SOS COVERAGE CONTRACT (STRICT):", team.SystemPrompt);
        Assert.Contains("RESCUE, MEDICAL_AID, or EVACUATE", team.SystemPrompt);
        Assert.Contains("description-only SOS mentions", team.SystemPrompt);

        Assert.Contains("IMPORTANT SOS COVERAGE CONTRACT (STRICT):", validation.SystemPrompt);
        Assert.Contains("IMPORTANT ITEM TYPE CONTRACT (STRICT):", validation.SystemPrompt);
        Assert.Contains("Reusable equipment must remain in COLLECT_SUPPLIES/RETURN_SUPPLIES", validation.SystemPrompt);
        Assert.Contains("DELIVER_SUPPLIES, RESCUE, MEDICAL_AID, or EVACUATE", validation.SystemPrompt);
        Assert.Contains("COLLECT_SUPPLIES, RETURN_SUPPLIES, RETURN_ASSEMBLY_POINT", validation.SystemPrompt);
        Assert.Contains("description-only SOS mentions", validation.SystemPrompt);
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

    [Fact]
    public void CreatePrompts_ActiveSosPriorityPrompt_UsesHundredPointAdjustmentContract()
    {
        var prompt = SystemSeeder.CreatePrompts()
            .Single(item => item.IsActive && item.PromptType == "SosPriorityAnalysis");

        Assert.Equal("v3.2", prompt.Version);
        Assert.Contains("0.0-100.0", prompt.SystemPrompt);
        Assert.Contains("điểm cuối cùng trên thang 0-100", prompt.SystemPrompt);
        Assert.Contains("score_adjustment_delta", prompt.SystemPrompt);
        Assert.Contains("rule_config", prompt.SystemPrompt);
        Assert.Contains("tiếng Việt", prompt.SystemPrompt);
        Assert.DoesNotContain("0.0-10.0", prompt.SystemPrompt);
    }
}
