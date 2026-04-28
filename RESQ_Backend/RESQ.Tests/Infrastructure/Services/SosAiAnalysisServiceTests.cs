using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Services;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Infrastructure.Services;

public class SosAiAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAndSaveAsync_PromptIncludesRuleConfigAndRuleEvaluation()
    {
        var client = new RecordingAiProviderClient(NoAdjustmentJson(50));
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 50, priority: SosPriorityLevel.Medium);

        await service.AnalyzeAndSaveAsync(task);

        var prompt = Assert.Single(client.LastRequest!.Messages).Content!;
        Assert.Contains("rule_config:", prompt);
        Assert.Contains("rule_based_evaluation:", prompt);
        Assert.Contains("sos_payload:", prompt);
        Assert.Contains("SOS_PRIORITY_TEST", prompt);
        Assert.Contains(@"""score"":50", prompt);
        Assert.Contains("tiếng Việt", prompt);
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_MissingAiScoreFallsBackToRuleBasedScore()
    {
        var client = new RecordingAiProviderClient("""
            {
              "suggested_priority": "Critical",
              "suggested_severity_level": "Critical",
              "explanation": "No numeric score was returned."
            }
            """);
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 50, priority: SosPriorityLevel.Medium);

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.Equal(50, analysis.SuggestedPriorityScore);
        Assert.Equal("Medium", analysis.SuggestedPriority);
        Assert.True(analysis.AgreesWithRuleBase);
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_ValidAdjustmentWithinGuardrailIsSaved()
    {
        var client = new RecordingAiProviderClient("""
            {
              "suggested_priority_score": 62,
              "suggested_severity_level": "Moderate",
              "score_adjustment_delta": 12,
              "adjustment_direction": "increase",
              "uncovered_factors": ["water rising faster than selected duration"],
              "rule_config_basis": ["P3 threshold 25"],
              "additional_severe_flag": false,
              "explanation": "Rule config supports a small increase due to extra details."
            }
            """);
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 50, priority: SosPriorityLevel.Medium);

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.Equal(62, analysis.SuggestedPriorityScore);
        Assert.False(analysis.AgreesWithRuleBase);
        using var metadata = JsonDocument.Parse(analysis.Metadata!);
        var result = metadata.RootElement.GetProperty("analysisResult");
        Assert.Equal(12, result.GetProperty("score_adjustment_delta").GetDouble());
        Assert.Equal("increase", result.GetProperty("adjustment_direction").GetString());
        Assert.False(result.GetProperty("guardrail_applied").GetBoolean());
        Assert.Contains("water rising", result.GetProperty("uncovered_factors")[0].GetString());
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_DoesNotAppendEnglishRuleBaseSuffix()
    {
        var client = new RecordingAiProviderClient("""
            {
              "suggested_priority_score": 37,
              "suggested_severity_level": "Critical",
              "agrees_with_rule_base": false,
              "needs_immediate_safe_transfer": true,
              "can_wait_for_combined_mission": false,
              "handling_reason": "Nạn nhân bất tỉnh nên cần xử lý ngay. AI suggested score 37.",
              "explanation": "Điểm theo luật chưa phản ánh đủ tình trạng bất tỉnh. AI suggested score 37. AI does not agree with the current rule-base score 22 (Low) and sees a different urgency level."
            }
            """);
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 22, priority: SosPriorityLevel.Low);

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.DoesNotContain("AI suggested", analysis.Explanation);
        Assert.DoesNotContain("rule-base score", analysis.Explanation);
        using var metadata = JsonDocument.Parse(analysis.Metadata!);
        var result = metadata.RootElement.GetProperty("analysisResult");
        Assert.DoesNotContain("AI suggested", result.GetProperty("explanation").GetString());
        Assert.DoesNotContain("rule-base score", result.GetProperty("explanation").GetString());
        Assert.DoesNotContain("AI suggested", result.GetProperty("handling_reason").GetString());
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_InvalidJsonFallbackExplanationIsVietnamese()
    {
        var client = new RecordingAiProviderClient("The victim is urgent but the response is not JSON.");
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 50, priority: SosPriorityLevel.Medium);

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.Contains("điểm", analysis.Explanation);
        Assert.Contains("luật", analysis.Explanation);
        Assert.DoesNotContain("AI suggested", analysis.Explanation);
        Assert.DoesNotContain("rule-base score", analysis.Explanation);
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_OversizedAdjustmentWithoutOverrideIsCapped()
    {
        var client = new RecordingAiProviderClient("""
            {
              "suggested_priority_score": 90,
              "suggested_severity_level": "Critical",
              "score_adjustment_delta": 40,
              "adjustment_direction": "increase",
              "uncovered_factors": ["general concern"],
              "additional_severe_flag": false,
              "explanation": "Large increase without concrete override."
            }
            """);
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(client, analysisRepo);
        var task = BuildTask(score: 50, priority: SosPriorityLevel.Medium);

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.Equal(65, analysis.SuggestedPriorityScore);
        Assert.Equal("Medium", analysis.SuggestedPriority);
        using var metadata = JsonDocument.Parse(analysis.Metadata!);
        var result = metadata.RootElement.GetProperty("analysisResult");
        Assert.True(result.GetProperty("guardrail_applied").GetBoolean());
        Assert.Equal(90, result.GetProperty("original_suggested_priority_score").GetDouble());
        Assert.Equal(15, result.GetProperty("score_adjustment_delta").GetDouble());
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_OversizedLifeThreateningOverrideIsAllowed()
    {
        var client = new RecordingAiProviderClient("""
            {
              "suggested_priority_score": 90,
              "suggested_severity_level": "Critical",
              "score_adjustment_delta": 40,
              "adjustment_direction": "increase",
              "uncovered_factors": ["victim unconscious"],
              "rule_config_basis": ["UNCONSCIOUS is severe medical issue"],
              "additional_severe_flag": true,
              "guardrail_override_reason": "Structured data includes UNCONSCIOUS, requiring immediate response.",
              "needs_immediate_safe_transfer": true,
              "can_wait_for_combined_mission": false,
              "explanation": "Life-threatening detail supports override."
            }
            """);
        var analysisRepo = new RecordingSosAiAnalysisRepository();
        var service = BuildService(
            client,
            analysisRepo,
            structuredData: """{"victims":[{"incident_status":{"medical_issues":["UNCONSCIOUS"]}}]}""");
        var task = BuildTask(
            score: 50,
            priority: SosPriorityLevel.Medium,
            structuredData: """{"victims":[{"incident_status":{"medical_issues":["UNCONSCIOUS"]}}]}""");

        await service.AnalyzeAndSaveAsync(task);

        var analysis = Assert.Single(analysisRepo.Created);
        Assert.Equal(90, analysis.SuggestedPriorityScore);
        Assert.Equal("Critical", analysis.SuggestedPriority);
        using var metadata = JsonDocument.Parse(analysis.Metadata!);
        var result = metadata.RootElement.GetProperty("analysisResult");
        Assert.False(result.GetProperty("guardrail_applied").GetBoolean());
        Assert.Equal(40, result.GetProperty("score_adjustment_delta").GetDouble());
    }

    private static SosAiAnalysisService BuildService(
        RecordingAiProviderClient client,
        RecordingSosAiAnalysisRepository analysisRepo,
        string structuredData = """{"incident":{"situation":"OTHER"}}""")
    {
        return new SosAiAnalysisService(
            new StubAiProviderClientFactory(client),
            new StubSettingsResolver(),
            new StubAiConfigRepository(),
            new StubPromptRepository(),
            new StubRuleConfigRepository(BuildRuleConfig()),
            analysisRepo,
            new StubSosRequestRepository(BuildSosRequest(structuredData)),
            new StubSosRequestUpdateRepository(),
            new StubUnitOfWork(),
            NullLogger<SosAiAnalysisService>.Instance);
    }

    private static SosAiAnalysisTask BuildTask(
        double score,
        SosPriorityLevel priority,
        string structuredData = """{"incident":{"situation":"OTHER"}}""")
    {
        return SosAiAnalysisTask.Create(
            99,
            structuredData,
            "Need support",
            "RELIEF",
            new SosRuleEvaluationModel
            {
                SosRequestId = 99,
                ConfigId = 7,
                ConfigVersion = "SOS_PRIORITY_TEST",
                RuleVersion = "SOS_PRIORITY_TEST",
                TotalScore = score,
                PriorityLevel = priority,
                BreakdownJson = JsonSerializer.Serialize(new SosPriorityEvaluationDetails
                {
                    ConfigId = 7,
                    ConfigVersion = "SOS_PRIORITY_TEST",
                    HasSevereFlag = false,
                    ThresholdDecision = new SosPriorityThresholdDecision
                    {
                        HasSevereFlag = false,
                        PriorityScore = score,
                        PriorityLevel = priority.ToString(),
                        P1Threshold = 70,
                        P2Threshold = 45,
                        P3Threshold = 25
                    }
                })
            });
    }

    private static SosRequestModel BuildSosRequest(string structuredData)
    {
        var request = SosRequestModel.Create(
            Guid.NewGuid(),
            new GeoLocation(10.0, 106.0),
            "Need support",
            sosType: "RELIEF",
            structuredData: structuredData);
        request.Id = 99;
        return request;
    }

    private static SosPriorityRuleConfigModel BuildRuleConfig()
    {
        var config = new SosPriorityRuleConfigDocument
        {
            ConfigVersion = "SOS_PRIORITY_TEST"
        };

        return new SosPriorityRuleConfigModel
        {
            Id = 7,
            ConfigVersion = "SOS_PRIORITY_TEST",
            IsActive = true,
            ConfigJson = SosPriorityRuleConfigSupport.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string NoAdjustmentJson(double score) => $$"""
        {
          "suggested_priority_score": {{score}},
          "suggested_severity_level": "Moderate",
          "score_adjustment_delta": 0,
          "adjustment_direction": "none",
          "uncovered_factors": [],
          "rule_config_basis": ["Rule-based score covers provided details"],
          "additional_severe_flag": false,
          "agrees_with_rule_base": true,
          "needs_immediate_safe_transfer": false,
          "can_wait_for_combined_mission": true,
          "explanation": "No extra factor beyond the rule-base score."
        }
        """;

    private sealed class RecordingAiProviderClient(string responseText) : IAiProviderClient
    {
        public AiProvider Provider => AiProvider.Gemini;
        public AiCompletionRequest? LastRequest { get; private set; }

        public Task<AiCompletionResponse> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AiCompletionResponse { Text = responseText, HttpStatusCode = 200 });
        }
    }

    private sealed class StubAiProviderClientFactory(IAiProviderClient client) : IAiProviderClientFactory
    {
        public IAiProviderClient GetClient(AiProvider provider) => client;
    }

    private sealed class StubSettingsResolver : IAiPromptExecutionSettingsResolver
    {
        public AiPromptExecutionSettings Resolve(AiConfigModel aiConfig)
            => new(AiProvider.Gemini, "test-model", "https://example.test", "key", 0.1, 1024);
    }

    private sealed class StubAiConfigRepository : IAiConfigRepository
    {
        public Task<AiConfigModel?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<AiConfigModel?>(new AiConfigModel
            {
                Id = 1,
                Provider = AiProvider.Gemini,
                Model = "test-model",
                ApiKey = "key",
                IsActive = true
            });

        public Task<AiConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AiConfigModel config, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsVersionAsync(string version, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AiConfigModel>> GetVersionsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateOthersAsync(int currentConfigId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AiConfigModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubPromptRepository : IPromptRepository
    {
        public Task<PromptModel?> GetActiveByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default)
            => Task.FromResult<PromptModel?>(new PromptModel
            {
                Id = 3,
                PromptType = PromptType.SosPriorityAnalysis,
                Version = "v-test",
                SystemPrompt = "system",
                UserPromptTemplate = "Analyze {{sos_type}} {{raw_message}} {{structured_data}}"
            });

        public Task<PromptModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(PromptModel prompt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsVersionAsync(PromptType promptType, string version, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PromptModel>> GetVersionsByTypeAsync(PromptType promptType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateOthersByTypeAsync(int currentPromptId, PromptType promptType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<PromptModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubRuleConfigRepository(SosPriorityRuleConfigModel model) : ISosPriorityRuleConfigRepository
    {
        public Task<SosPriorityRuleConfigModel?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<SosPriorityRuleConfigModel?>(model);

        public Task<SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<SosPriorityRuleConfigModel?>(id == model.Id ? model : null);

        public Task<IReadOnlyList<SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ExistsConfigVersionAsync(string configVersion, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class RecordingSosAiAnalysisRepository : ISosAiAnalysisRepository
    {
        public List<SosAiAnalysisModel> Created { get; } = [];
        public Task CreateAsync(SosAiAnalysisModel analysis, CancellationToken cancellationToken = default)
        {
            Created.Add(analysis);
            return Task.CompletedTask;
        }

        public Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, SosAiAnalysisModel>> GetLatestBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestRepository(SosRequestModel sosRequest) : ISosRequestRepository
    {
        public Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<SosRequestModel?>(id == sosRequest.Id ? sosRequest : null);

        public Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubSosRequestUpdateRepository : ISosRequestUpdateRepository
    {
        public Task<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>> GetLatestVictimUpdatesBySosRequestIdsAsync(
            IEnumerable<int> sosRequestIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, SosRequestVictimUpdateModel>>(
                new Dictionary<int, SosRequestVictimUpdateModel>());

        public Task AddVictimUpdateAsync(SosRequestVictimUpdateModel update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddIncidentRangeAsync(IEnumerable<SosRequestIncidentUpdateModel> updates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetSosRequestIdsByTeamIncidentIdsAsync(IEnumerable<int> teamIncidentIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetTeamIncidentIdsBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyList<SosRequestIncidentUpdateModel>>> GetIncidentHistoryBySosRequestIdsAsync(IEnumerable<int> sosRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
