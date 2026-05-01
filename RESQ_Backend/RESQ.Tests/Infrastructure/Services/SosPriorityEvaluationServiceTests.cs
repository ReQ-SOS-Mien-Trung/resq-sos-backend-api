using System.Text.Json;
using RESQ.Application.Common;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Services;

namespace RESQ.Tests.Infrastructure.Services;

public class SosPriorityEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateAsync_MobileCriticalMixedPayload_PromotesToCriticalAndUsesNewSignals()
    {
        var service = new SosPriorityEvaluationService(new StubRuleConfigRepository(BuildMinimalActiveConfig()));

        var evaluation = await service.EvaluateAsync(
            21,
            CriticalMixedPayloadJson,
            "BOTH");

        Assert.Equal(SosPriorityLevel.Critical, evaluation.PriorityLevel);

        var breakdown = JsonSerializer.Deserialize<SosPriorityEvaluationDetails>(evaluation.BreakdownJson!)!;
        Assert.True(breakdown.CriticalSeverityFlag);
        Assert.True(breakdown.UrgentMedicineFlag);
        Assert.Equal(40, breakdown.RequestTypeScore);
        Assert.False(breakdown.AreBlanketsEnough);
        Assert.Equal(3, breakdown.BlanketRequestCount);
        Assert.Equal(3, breakdown.BlanketUrgencyScore);
        Assert.True(breakdown.MedicineUrgencyScore > 0);
        Assert.True(breakdown.ReliefPressureMultiplier > 1);
    }

    [Fact]
    public async Task EvaluateWithConfigAsync_CriticalEscalatorCanOverrideScoreBelowP1Threshold()
    {
        var config = new SosPriorityRuleConfigDocument
        {
            PriorityScore = new SosPriorityScoreConfig
            {
                Formula = "40",
                UseRequestTypeScore = false,
                Expression = SosExpressionNode.Constant(40)
            }
        };

        var service = new SosPriorityEvaluationService(new StubRuleConfigRepository(BuildConfig(config)));

        var evaluation = await service.EvaluateAsync(
            22,
            CriticalMixedPayloadJson,
            "BOTH");

        Assert.Equal(40, evaluation.TotalScore);
        Assert.Equal(SosPriorityLevel.Critical, evaluation.PriorityLevel);

        var breakdown = JsonSerializer.Deserialize<SosPriorityEvaluationDetails>(evaluation.BreakdownJson!)!;
        Assert.True(breakdown.EscalationDecision!.Applied);
        Assert.Equal("Medium", breakdown.EscalationDecision.OriginalPriorityLevel);
        Assert.Equal("Critical", breakdown.EscalationDecision.FinalPriorityLevel);
        Assert.Contains("critical_severity_with_danger_or_urgent_or_vulnerable_min_CRITICAL", breakdown.EscalationDecision.Reasons);
    }

    private static SosPriorityRuleConfigModel BuildMinimalActiveConfig()
    {
        return new SosPriorityRuleConfigModel
        {
            Id = 1,
            ConfigVersion = "SOS_PRIORITY_DEMO_V1",
            IsActive = true,
            ConfigJson = """{"levels":["Low","Medium","High","Critical"]}"""
        };
    }

    private static SosPriorityRuleConfigModel BuildConfig(SosPriorityRuleConfigDocument config)
    {
        return new SosPriorityRuleConfigModel
        {
            Id = 2,
            ConfigVersion = "SOS_PRIORITY_TEST",
            IsActive = true,
            ConfigJson = SosPriorityRuleConfigSupport.Serialize(config)
        };
    }

    private sealed class StubRuleConfigRepository(SosPriorityRuleConfigModel config) : ISosPriorityRuleConfigRepository
    {
        public Task<SosPriorityRuleConfigModel?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<SosPriorityRuleConfigModel?>(config);

        public Task<IReadOnlyList<SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SosPriorityRuleConfigModel>>([config]);

        public Task<SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<SosPriorityRuleConfigModel?>(id == config.Id ? config : null);

        public Task<bool> ExistsConfigVersionAsync(string configVersion, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(config.ConfigVersion, configVersion, StringComparison.OrdinalIgnoreCase));

        public Task CreateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private const string CriticalMixedPayloadJson = """
        {
          "incident": {
            "situation": "TRAPPED",
            "has_injured": true,
            "need_medical": true,
            "people_count": {
              "adult": 1,
              "child": 1,
              "elderly": 1
            }
          },
          "group_needs": {
            "water": {
              "duration": "6_TO_12H"
            },
            "food": {
              "duration": "12_TO_24H"
            },
            "blanket": {
              "availability": "NOT_ENOUGH",
              "is_cold_or_wet": true
            },
            "clothing": {
              "status": "PARTIALLY_LACKING"
            },
            "medicine": {
              "needs_urgent_medicine": true,
              "conditions": ["CHRONIC_DISEASE", "INJURED"],
              "medical_needs": ["COMMON_MEDICINE", "FIRST_AID"]
            },
            "supplies": ["WATER", "FOOD", "CLOTHES", "BLANKET", "MEDICINE", "OTHER"]
          },
          "victims": [
            {
              "person_type": "ADULT",
              "incident_status": {
                "severity": "CRITICAL",
                "is_injured": true,
                "medical_issues": ["SEVERELY_BLEEDING", "HEAD_INJURY"]
              },
              "personal_needs": {
                "clothing": {
                  "needed": false
                }
              }
            },
            {
              "person_type": "CHILD",
              "incident_status": {
                "is_injured": false,
                "medical_issues": []
              },
              "personal_needs": {
                "clothing": {
                  "needed": true
                }
              }
            },
            {
              "person_type": "ELDERLY",
              "incident_status": {
                "is_injured": false,
                "medical_issues": []
              },
              "personal_needs": {
                "clothing": {
                  "needed": false
                }
              }
            }
          ]
        }
        """;
}
