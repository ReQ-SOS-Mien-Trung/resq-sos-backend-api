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
        Assert.Equal(10, breakdown.RequestTypeScore);
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

    [Fact]
    public async Task EvaluateAsync_ProvidedHighMedicalPayload_UsesDefaultFormulaWithoutDoubleMedicalWeight()
    {
        var service = new SosPriorityEvaluationService(new StubRuleConfigRepository(BuildMinimalActiveConfig()));

        var evaluation = await service.EvaluateAsync(
            21,
            ProvidedHighMedicalPayloadJson,
            "BOTH");

        Assert.Equal(88, evaluation.TotalScore);
        Assert.Equal(SosPriorityLevel.Critical, evaluation.PriorityLevel);

        var breakdown = JsonSerializer.Deserialize<SosPriorityEvaluationDetails>(evaluation.BreakdownJson!)!;
        Assert.True(breakdown.MedicalSevereFlag);
        Assert.False(breakdown.CriticalSeverityFlag);
        Assert.True(breakdown.DangerousSituationFlag);
        Assert.True(breakdown.UrgentMedicineFlag);
        Assert.True(breakdown.HasVulnerablePeople);
        Assert.Equal(1, breakdown.RawVariables["medical_weight"]);
        Assert.Equal(1.1, breakdown.RawVariables["relief_weight"]);
        Assert.Equal(0.15, breakdown.RawVariables["request_type_weight"]);
        Assert.Equal("Critical", breakdown.ThresholdDecision!.PriorityLevel);
    }

    [Fact]
    public async Task EvaluateWithConfigAsync_ProvidedHighMedicalPayload_EscalatesToCriticalBelowP1Threshold()
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
            21,
            ProvidedHighMedicalPayloadJson,
            "BOTH");

        Assert.Equal(40, evaluation.TotalScore);
        Assert.Equal(SosPriorityLevel.Critical, evaluation.PriorityLevel);

        var breakdown = JsonSerializer.Deserialize<SosPriorityEvaluationDetails>(evaluation.BreakdownJson!)!;
        Assert.True(breakdown.MedicalSevereFlag);
        Assert.False(breakdown.CriticalSeverityFlag);
        Assert.True(breakdown.DangerousSituationFlag);
        Assert.True(breakdown.UrgentMedicineFlag);
        Assert.True(breakdown.HasVulnerablePeople);
        Assert.True(breakdown.EscalationDecision!.Applied);
        Assert.Equal("Medium", breakdown.EscalationDecision.OriginalPriorityLevel);
        Assert.Equal("Critical", breakdown.EscalationDecision.FinalPriorityLevel);
        Assert.Contains(
            "high_or_severe_severity_danger_with_vulnerable_or_urgent_or_relief_pressure_min_CRITICAL",
            breakdown.EscalationDecision.Reasons);
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

    private const string ProvidedHighMedicalPayloadJson = """
        {
          "incident": {
            "situation": "TRAPPED",
            "other_situation_description": null,
            "address": "2 Trần Hưng Đạo, Phú Hòa, Thành phố Huế",
            "additional_description": "Nước dâng cao hung, ông Khoa già đang bị lạnh run, mệt lả. Cứu gấp với mấy anh ơi!\nThông tin y tế nền: Khoa (Dị ứng: Dị ứng bụi; Tiền sử chấn thương / phẫu thuật: ghi chú: Đôi khi đau nửa đầu khi thiếu ngủ.; Ghi chú y tế nền: Em trai thường đi làm xa, cần báo sớm khi có sơ tán.; Yêu cầu đặc biệt: Cần hỗ trợ định vị nếu mất sóng điện thoại.)",
            "people_count": {
              "adult": 1,
              "child": 1,
              "elderly": 0
            },
            "has_injured": true,
            "others_are_stable": null,
            "can_move": null,
            "need_medical": true,
            "has_pregnant_any": null,
            "other_medical_description": null
          },
          "group_needs": {
            "supplies": [
              "WATER",
              "FOOD",
              "CLOTHES",
              "BLANKET",
              "MEDICINE",
              "OTHER"
            ],
            "water": {
              "duration": "6_TO_12H",
              "remaining": null
            },
            "food": {
              "duration": "12_TO_24H"
            },
            "blanket": {
              "is_cold_or_wet": true,
              "are_blankets_enough": null,
              "availability": "NOT_ENOUGH",
              "request_count": null
            },
            "medicine": {
              "needs_urgent_medicine": true,
              "conditions": [
                "CHRONIC_DISEASE",
                "INJURED"
              ],
              "other_description": null,
              "medical_needs": [
                "COMMON_MEDICINE",
                "FIRST_AID"
              ],
              "medical_description": null
            },
            "clothing": {
              "status": "PARTIALLY_LACKING",
              "needed_people_count": null
            },
            "other_supply_description": "Pin sạc dự phòng"
          },
          "victims": [
            {
              "person_id": "relative_7182C4AB-223F-C1BE-F7A6-3935258E6377",
              "person_type": "ADULT",
              "index": 1,
              "custom_name": "Khoa",
              "person_phone": "+84911224567",
              "need_rescue": true,
              "incident_status": {
                "is_injured": true,
                "severity": "HIGH",
                "medical_issues": [
                  "CONFUSION",
                  "HEAD_INJURY",
                  "CANNOT_MOVE"
                ]
              },
              "personal_needs": {
                "clothing": {
                  "needed": true,
                  "gender": "MALE"
                },
                "diet": {
                  "has_special_diet": true,
                  "description": "Dị ứng thịt"
                }
              }
            },
            {
              "person_id": "manual_child_1",
              "person_type": "CHILD",
              "index": 1,
              "custom_name": "Thảo",
              "person_phone": null,
              "need_rescue": true,
              "incident_status": {
                "is_injured": true,
                "severity": "HIGH",
                "medical_issues": [
                  "LOST_PARENT"
                ]
              },
              "personal_needs": {
                "clothing": {
                  "needed": false,
                  "gender": null
                },
                "diet": {
                  "has_special_diet": true,
                  "description": "Cần sữa"
                }
              }
            }
          ],
          "prepared_profiles": null
        }
        """;
}
