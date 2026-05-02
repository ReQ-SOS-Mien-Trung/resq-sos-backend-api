using System.Globalization;
using System.Text.Json;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private const int HueStadiumUnclusteredSosCount = 10;
    private const int HueStadiumSosClusterCount = 11;
    private const int HueStadiumSosRequestCount = 20;

    private async Task SeedEmergencyAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var (clusterScenarios, sosScenarios) = CreateHueStadiumSosScenarios();
        if (clusterScenarios.Count != HueStadiumSosClusterCount || sosScenarios.Count != HueStadiumSosRequestCount)
        {
            throw new InvalidOperationException(
                $"Hue stadium SOS seed must contain exactly {HueStadiumSosClusterCount} clusters and {HueStadiumSosRequestCount} SOS requests.");
        }

        var createdSos = new List<SosRequest>();

        for (var i = 0; i < clusterScenarios.Count; i++)
        {
            var scenario = clusterScenarios[i];
            var createdAt = VnToUtc(scenario.LocalCreatedAt);
            var cluster = new SosCluster
            {
                CenterLocation = Point(scenario.Longitude, scenario.Latitude),
                RadiusKm = scenario.RadiusKm,
                SeverityLevel = scenario.SeverityLevel,
                WaterLevel = scenario.WaterLevel,
                VictimEstimated = scenario.VictimEstimated,
                ChildrenCount = scenario.ChildrenCount,
                ElderlyCount = scenario.ElderlyCount,
                MedicalUrgencyScore = scenario.MedicalUrgencyScore,
                CreatedAt = createdAt,
                LastUpdatedAt = ClampHistoricalUtc(createdAt.AddHours(1), createdAt, seed.AnchorUtc),
                Status = scenario.Status
            };

            seed.SosClusters.Add(cluster);
        }

        for (var i = 0; i < sosScenarios.Count; i++)
        {
            var scenario = sosScenarios[i];
            var cluster = seed.SosClusters[scenario.ClusterIndex];
            var victim = seed.Victims[scenario.VictimIndex % seed.Victims.Count];
            var reporter = seed.Victims[scenario.ReporterIndex % seed.Victims.Count];
            var coordinator = seed.Coordinators[scenario.CoordinatorIndex % seed.Coordinators.Count];
            var createdAt = VnToUtc(scenario.LocalCreatedAt);
            var receivedAt = ClampHistoricalUtc(createdAt.AddMinutes(1 + i % 4), createdAt, seed.AnchorUtc);
            DateTime? reviewedAt = scenario.Status == SosRequestStatus.Pending.ToString()
                ? null
                : ClampHistoricalUtc(receivedAt.AddMinutes(6 + i % 8), receivedAt, seed.AnchorUtc);
            var lastUpdatedAt = ClampHistoricalUtc(
                reviewedAt?.AddMinutes(scenario.Status == SosRequestStatus.Resolved.ToString() ? 160 + i * 4 : 24 + i * 3)
                    ?? receivedAt.AddMinutes(8 + i),
                reviewedAt ?? receivedAt,
                seed.AnchorUtc);
            var packetId = StableGuid($"packet-hue-tu-do-{i + 1:000}");
            var deviceId = StableGuid($"device-hue-tu-do-{i + 1:000}").ToString().ToUpperInvariant();

            createdSos.Add(new SosRequest
            {
                PacketId = packetId,
                Cluster = cluster,
                UserId = victim.Id,
                Location = Point(scenario.Longitude, scenario.Latitude),
                LocationAccuracy = 6 + i % 9,
                SosType = scenario.SosType,
                RawMessage = BuildHueStadiumRawMessage(scenario),
                StructuredData = BuildHueStadiumStructuredData(scenario),
                NetworkMetadata = BuildHueStadiumNetworkMetadata(scenario, deviceId),
                SenderInfo = BuildHueStadiumSenderInfo(victim, reporter, coordinator, scenario, deviceId),
                VictimInfo = null, // Mobile luôn gửi victim_info=null; BE link victim qua structured_data.victims[].person_phone.
                ReporterInfo = BuildHueStadiumReporterInfo(victim, reporter, coordinator, scenario, deviceId),
                IsSentOnBehalf = scenario.IsSentOnBehalf,
                OriginId = deviceId,
                PriorityLevel = scenario.PriorityLevel,
                PriorityScore = scenario.PriorityScore,
                Status = scenario.Status,
                AiAnalysis = null,
                ReceivedAt = receivedAt,
                Timestamp = new DateTimeOffset(createdAt).ToUnixTimeSeconds(),
                CreatedAt = createdAt,
                LastUpdatedAt = lastUpdatedAt,
                ReviewedAt = reviewedAt,
                ReviewedById = scenario.Status == SosRequestStatus.Pending.ToString() ? null : coordinator.Id,
                CreatedByCoordinatorId = scenario.IsSentOnBehalf ? coordinator.Id : null
            });
        }

        foreach (var cluster in seed.SosClusters)
        {
            var clusterSos = createdSos.Where(sos => ReferenceEquals(sos.Cluster, cluster)).ToList();
            if (clusterSos.Count == 0)
            {
                continue;
            }

            cluster.LastUpdatedAt = clusterSos
                .Select(sos => sos.LastUpdatedAt ?? sos.CreatedAt ?? cluster.CreatedAt ?? seed.AnchorUtc)
                .Max();
        }

        _db.SosClusters.AddRange(seed.SosClusters);
        _db.SosRequests.AddRange(createdSos);
        await _db.SaveChangesAsync(cancellationToken);
        seed.SosRequests.AddRange(createdSos);

        var companions = new List<SosRequestCompanion>();
        for (var i = 0; i < seed.SosRequests.Count; i++)
        {
            var sos = seed.SosRequests[i];
            var companionCount = 1 + i % 3;
            for (var j = 0; j < companionCount; j++)
            {
                var companion = seed.Victims[(i * 5 + j * 11 + 30) % seed.Victims.Count];
                if (companion.Id == sos.UserId)
                {
                    companion = seed.Victims[(i * 5 + j * 11 + 31) % seed.Victims.Count];
                }

                companions.Add(new SosRequestCompanion
                {
                    SosRequestId = sos.Id,
                    UserId = companion.Id,
                    PhoneNumber = companion.Phone,
                    AddedAt = ClampHistoricalUtc(
                        (sos.CreatedAt ?? seed.StartUtc).AddMinutes(4 + j * 3),
                        sos.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
                });
            }
        }
        _db.SosRequestCompanions.AddRange(companions.GroupBy(c => new { c.SosRequestId, c.UserId }).Select(g => g.First()));

        foreach (var sos in seed.SosRequests)
        {
            var createdAt = sos.CreatedAt ?? seed.StartUtc;
            _db.SosRuleEvaluations.Add(new SosRuleEvaluation
            {
                SosRequestId = sos.Id,
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                MedicalScore = sos.PriorityLevel is "Critical" ? 9 : sos.PriorityLevel is "High" ? 7 : 4,
                FoodScore = (sos.Id % 5) + 2,
                InjuryScore = sos.RawMessage?.Contains("bị thương", StringComparison.OrdinalIgnoreCase) == true ? 8 : 1,
                MobilityScore = sos.RawMessage?.Contains("không thể di chuyển", StringComparison.OrdinalIgnoreCase) == true ? 9 : 4,
                EnvironmentScore = sos.PriorityLevel is "Critical" ? 9 : 5,
                TotalScore = sos.PriorityScore,
                PriorityLevel = sos.PriorityLevel,
                RuleVersion = "v1.0",
                ItemsNeeded = BuildHueStadiumRuleItemsNeeded(sos),
                BreakdownJson = Json(new { priority = sos.PriorityLevel, reason = "Curated Hue stadium mobile SOS demo seed" }),
                DetailsJson = sos.StructuredData,
                CreatedAt = ClampHistoricalUtc(createdAt.AddMinutes(1), createdAt, seed.AnchorUtc)
            });

            for (var u = 0; u < 2; u++)
            {
                _db.SosRequestUpdates.Add(new SosRequestUpdate
                {
                    SosRequestId = sos.Id,
                    Type = u == 0 ? "CoordinatorNote" : sos.Status == "Resolved" ? "Rescued" : "TeamApproaching",
                    Content = u == 0 ? "Đã tiếp nhận thông tin và kiểm tra vị trí." : SosUpdateContent(sos.Status),
                    CreatedAt = ClampHistoricalUtc(createdAt.AddMinutes(15 + u * 35), createdAt, seed.AnchorUtc),
                    Status = "Visible"
                });
            }
        }

        foreach (var sos in seed.SosRequests)
        {
            var aiAnalysis = BuildHueStadiumSeedAiAnalysis(sos);
            _db.SosAiAnalyses.Add(new SosAiAnalysis
            {
                SosRequestId = sos.Id,
                ModelName = "DemoSeed.SosPriorityAnalysis",
                ModelVersion = "v3.1",
                AnalysisType = "SosPriorityAnalysis",
                SuggestedSeverityLevel = aiAnalysis.SeverityLevel,
                SuggestedPriority = aiAnalysis.Priority,
                SuggestedPriorityScore = aiAnalysis.Score,
                AgreesWithRuleBase = aiAnalysis.AgreesWithRuleBase,
                Explanation = aiAnalysis.Explanation,
                SuggestionScope = "DemoSeed:SosPriorityAnalysis v3.1",
                Metadata = BuildHueStadiumSeedAiMetadata(sos, aiAnalysis),
                CreatedAt = ClampHistoricalUtc(
                    (sos.CreatedAt ?? seed.StartUtc).AddMinutes(2),
                    sos.CreatedAt ?? seed.StartUtc,
                    seed.AnchorUtc),
                AdoptedAt = sos.Status == "Pending"
                    ? null
                    : ClampHistoricalUtc(
                        (sos.ReviewedAt ?? sos.CreatedAt)?.AddMinutes(1),
                        sos.ReviewedAt ?? sos.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static HueStadiumSeedAiAnalysis BuildHueStadiumSeedAiAnalysis(SosRequest sos)
    {
        var priority = NormalizeHueStadiumAiPriority(sos.PriorityLevel);
        var score = Math.Round(sos.PriorityScore ?? HueStadiumFallbackPriorityScore(priority), 2);
        var severityLevel = HueStadiumSeverityForPriority(priority);
        var isClosed = IsHueStadiumClosedSos(sos);
        var needsImmediateSafeTransfer = !isClosed && HueStadiumNeedsImmediateSafeTransfer(sos, priority);
        var canWaitForCombinedMission = !needsImmediateSafeTransfer;
        var ruleConfigBasis = BuildHueStadiumSeedAiRuleBasis(sos, score, priority);
        var handlingReason = BuildHueStadiumSeedAiHandlingReason(sos, needsImmediateSafeTransfer, isClosed);
        var explanation = BuildHueStadiumSeedAiExplanation(score, priority, ruleConfigBasis);

        return new HueStadiumSeedAiAnalysis(
            Priority: priority,
            SeverityLevel: severityLevel,
            Score: score,
            AgreesWithRuleBase: true,
            NeedsImmediateSafeTransfer: needsImmediateSafeTransfer,
            CanWaitForCombinedMission: canWaitForCombinedMission,
            HandlingReason: handlingReason,
            Explanation: explanation,
            RuleConfigBasis: ruleConfigBasis);
    }

    private static string BuildHueStadiumSeedAiMetadata(SosRequest sos, HueStadiumSeedAiAnalysis analysis)
    {
        var analysisResult = new Dictionary<string, object?>
        {
            ["priority"] = analysis.Priority,
            ["suggested_priority"] = analysis.Priority,
            ["severity_level"] = analysis.SeverityLevel,
            ["suggested_severity_level"] = analysis.SeverityLevel,
            ["suggested_priority_score"] = analysis.Score,
            ["agrees_with_rule_base"] = analysis.AgreesWithRuleBase,
            ["score_adjustment_delta"] = 0.0,
            ["adjustment_direction"] = "none",
            ["uncovered_factors"] = Array.Empty<string>(),
            ["rule_config_basis"] = analysis.RuleConfigBasis,
            ["additional_severe_flag"] = false,
            ["guardrail_override_reason"] = null,
            ["needs_immediate_safe_transfer"] = analysis.NeedsImmediateSafeTransfer,
            ["can_wait_for_combined_mission"] = analysis.CanWaitForCombinedMission,
            ["handling_reason"] = analysis.HandlingReason,
            ["explanation"] = analysis.Explanation
        };

        return Json(new Dictionary<string, object?>
        {
            ["rawResponse"] = Json(analysisResult),
            ["analysisResult"] = analysisResult,
            ["promptType"] = "SosPriorityAnalysis",
            ["promptVersion"] = "v3.1",
            ["provider"] = "DemoSeed",
            ["contentFingerprint"] = $"demo-seed-sos-{sos.Id}-sos-priority-analysis-v3.1",
            ["adjustmentContract"] = new Dictionary<string, object?>
            {
                ["scoreScale"] = "0-100",
                ["defaultAdjustmentLimit"] = 15,
                ["ruleBasedBaselineRequired"] = true
            },
            ["ruleBaseContext"] = new Dictionary<string, object?>
            {
                ["score"] = sos.PriorityScore,
                ["priority"] = sos.PriorityLevel,
                ["ruleVersion"] = "v1.0",
                ["configVersion"] = "SOS_PRIORITY_DEMO_V1",
                ["baselineSource"] = "sos_requests.priority_score"
            },
            ["seedArea"] = "Huế demo SOS",
            ["mobilePacket"] = true
        });
    }

    private static string NormalizeHueStadiumAiPriority(string? priority) => priority switch
    {
        "Critical" => "Critical",
        "High" => "High",
        "Medium" => "Medium",
        "Low" => "Low",
        _ => "Medium"
    };

    private static double HueStadiumFallbackPriorityScore(string priority) => priority switch
    {
        "Critical" => 90,
        "High" => 75,
        "Medium" => 55,
        "Low" => 30,
        _ => 50
    };

    private static string HueStadiumSeverityForPriority(string priority) => priority switch
    {
        "Critical" => "Critical",
        "High" => "Severe",
        "Medium" => "Moderate",
        "Low" => "Minor",
        _ => "Moderate"
    };

    private static bool IsHueStadiumClosedSos(SosRequest sos)
    {
        if (sos.Status is "Resolved" or "Cancelled")
        {
            return true;
        }

        return HueStadiumSosTextContains(sos, "đã được", "hiện an toàn", "xin hủy", "không còn yêu cầu", "đã nhận");
    }

    private static bool HueStadiumNeedsImmediateSafeTransfer(SosRequest sos, string priority)
    {
        if (priority == "Critical")
        {
            return true;
        }

        if (!HueStadiumIsRescueLike(sos.SosType))
        {
            return false;
        }

        return HueStadiumSosTextContains(
            sos,
            "mắc kẹt",
            "không thể",
            "dây điện",
            "khó thở",
            "mang thai",
            "bị cô lập",
            "cửa cuốn",
            "nước chảy mạnh",
            "vùng nguy hiểm",
            "hạ thân nhiệt",
            "gãy tay",
            "cần xuồng",
            "nước xoáy");
    }

    private static bool HueStadiumIsRescueLike(string? sosType) =>
        string.Equals(sosType, "Rescue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sosType, "Both", StringComparison.OrdinalIgnoreCase);

    private static bool HueStadiumSosTextContains(SosRequest sos, params string[] tokens)
    {
        var text = $"{sos.RawMessage} {sos.StructuredData}".ToLowerInvariant();
        return tokens.Any(token => text.Contains(token.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static List<string> BuildHueStadiumSeedAiRuleBasis(SosRequest sos, double score, string priority)
    {
        var basis = new List<string>
        {
            $"Điểm rule-base hiện tại là {FormatHueStadiumScore(score)}/100 và mức ưu tiên hiện tại là {priority}."
        };

        if (HueStadiumSosTextContains(sos, "bị thương", "gãy tay", "khó thở", "hạ thân nhiệt", "mang thai", "thuốc"))
        {
            basis.Add("Nội dung SOS có yếu tố y tế hoặc nhóm dễ tổn thương nên không hạ thấp mức đánh giá.");
        }

        if (HueStadiumSosTextContains(sos, "mắc kẹt", "bị cô lập", "không thể", "cửa cuốn", "cần xuồng", "dây điện", "nước xoáy"))
        {
            basis.Add("Tin nhắn có dấu hiệu hạn chế tự di chuyển hoặc đường tiếp cận nguy hiểm.");
        }

        if (HueStadiumSosTextContains(sos, "nước", "cháo", "thuốc", "đèn pin", "mì", "chăn"))
        {
            basis.Add("Nhu cầu cứu trợ được ghi rõ trong raw_message và structured_data.");
        }

        if (IsHueStadiumClosedSos(sos))
        {
            basis.Add("Trạng thái hoặc nội dung cho thấy yêu cầu đã được xử lý/hủy, nhận định AI chỉ giữ mục đích hậu kiểm demo.");
        }

        return basis;
    }

    private static string BuildHueStadiumSeedAiHandlingReason(
        SosRequest sos,
        bool needsImmediateSafeTransfer,
        bool isClosed)
    {
        if (isClosed)
        {
            return "Yêu cầu đã được xử lý hoặc hủy trong nội dung SOS nên không cần điều phối khẩn mới; có thể dùng cho theo dõi và hậu kiểm.";
        }

        if (needsImmediateSafeTransfer)
        {
            return "Không nên chờ ghép mission vì nội dung có dấu hiệu mắc kẹt, nguy cơ y tế hoặc đường tiếp cận nguy hiểm; cần đội tiếp cận sớm để đưa người về nơi an toàn.";
        }

        if (string.Equals(sos.SosType, "Relief", StringComparison.OrdinalIgnoreCase))
        {
            return "Có thể chờ ghép mission cứu trợ vì nhu cầu chính là nước, thực phẩm, thuốc hoặc vật tư và chưa có dấu hiệu phải di chuyển ngay.";
        }

        return "Có thể ghép mission mixed nếu cùng khu vực vì nhóm đang ở vị trí tạm ổn định và chưa có bằng chứng đe dọa tính mạng tức thời.";
    }

    private static string BuildHueStadiumSeedAiExplanation(
        double score,
        string priority,
        IReadOnlyList<string> ruleConfigBasis)
    {
        var scoreText = FormatHueStadiumScore(score);
        var basisText = string.Join(" ", ruleConfigBasis);
        return $"Điểm AI đề xuất {scoreText}/100, giữ nguyên mức {priority}. {basisText} AI đồng ý với điểm rule-base {scoreText}/100 và không điều chỉnh thêm vì không có yếu tố ngoài rule_config cần tăng hoặc giảm điểm theo prompt SosPriorityAnalysis v3.1.";
    }

    private static string FormatHueStadiumScore(double score) =>
        score.ToString("0.##", CultureInfo.InvariantCulture);

    private static string BuildHueStadiumStructuredData(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var hasPregnant = scenario.Victims.Any(v => string.Equals(v.PersonType, "PREGNANT", StringComparison.Ordinal));
        var payload = new Dictionary<string, object?>
        {
            ["incident"] = new Dictionary<string, object?>
            {
                ["address"] = scenario.Address,
                ["people_count"] = new
                {
                    adult = peopleCount.Adult,
                    child = peopleCount.Child,
                    elderly = peopleCount.Elderly
                },
                ["situation"] = scenario.Situation,
                ["can_move"] = scenario.CanMove,
                ["has_injured"] = scenario.HasInjured,
                ["need_medical"] = scenario.NeedMedical,
                ["others_are_stable"] = scenario.OthersAreStable,
                ["has_pregnant_any"] = hasPregnant,
                ["additional_description"] = scenario.AdditionalDescription
            },
            ["victims"] = scenario.Victims
                .Select((victim, index) => new Dictionary<string, object?>
                {
                    ["person_id"] = victim.PersonId,
                    ["person_type"] = victim.PersonType,
                    ["index"] = index + 1,
                    ["custom_name"] = victim.CustomName,
                    ["incident_status"] = BuildHueStadiumVictimIncidentStatus(victim),
                    ["personal_needs"] = BuildHueStadiumVictimPersonalNeeds(victim)
                })
                .ToList()
        };

        if (scenario.GroupNeeds.Count > 0)
        {
            payload["group_needs"] = BuildHueStadiumGroupNeeds(scenario);
        }

        return Json(payload);
    }

    private static string BuildHueStadiumNetworkMetadata(
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        return Json(new
        {
            hop_count = scenario.Network == "MESH" ? 1 : 0,
            path = new[] { deviceId }
        });
    }

    private static string BuildHueStadiumSenderInfo(
        User victim,
        User reporter,
        User coordinator,
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        var sender = scenario.IsSentOnBehalf ? coordinator : reporter;
        return Json(new
        {
            device_id = deviceId,
            is_online = scenario.Network != "MESH" && !scenario.IsSentOnBehalf,
            user_id = sender.Id,
            user_name = FullName(sender),
            user_phone = sender.Phone,
            battery_level = scenario.BatteryPercentage
        });
    }

    private static string BuildHueStadiumVictimInfo(User victim, HueStadiumSosScenario scenario)
    {
        return Json(new
        {
            user_id = victim.Id,
            user_name = FullName(victim),
            user_phone = victim.Phone
        });
    }

    private static string BuildHueStadiumReporterInfo(
        User victim,
        User reporter,
        User coordinator,
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        var reporterUser = scenario.IsSentOnBehalf ? coordinator : reporter;
        return Json(new
        {
            device_id = deviceId,
            is_online = scenario.Network != "MESH" && !scenario.IsSentOnBehalf,
            user_id = reporterUser.Id,
            user_name = FullName(reporterUser),
            user_phone = reporterUser.Phone,
            battery_level = scenario.BatteryPercentage
        });
    }

    private static string BuildHueStadiumRawMessage(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var totalPeople = peopleCount.Adult + peopleCount.Child + peopleCount.Elderly;
        var injuredVictims = scenario.Victims
            .Select((victim, index) => (Victim: victim, Index: index + 1, MedicalIssues: HueStadiumMedicalIssuesForVictim(victim)))
            .Where(item => IsHueStadiumVictimInjured(item.Victim, item.MedicalIssues))
            .Select(item => $"{HueStadiumPersonTypeLabel(item.Victim.PersonType)} {item.Index}: {item.Victim.CustomName} - {HueStadiumMedicalIssueLabel(item.MedicalIssues.FirstOrDefault())}")
            .ToList();
        var injuredText = injuredVictims.Count == 0
            ? "Không"
            : string.Join("; ", injuredVictims);

        return $"{HueStadiumSosTypeLabel(scenario.SosType)} | Tình trạng: {HueStadiumSituationLabel(scenario.Situation)} | Số người: {totalPeople} | Bị thương: {injuredText} | Ghi chú: {scenario.AdditionalDescription}";
    }

    private static object BuildHueStadiumGroupNeeds(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var totalPeople = peopleCount.Adult + peopleCount.Child + peopleCount.Elderly;
        // Map scenario GroupNeeds (internal keys) → mobile SupplyNeed enum (WATER, FOOD, CLOTHES, BLANKET, MEDICINE, OTHER)
        var mobileSupplies = scenario.GroupNeeds
            .Select(s => s switch
            {
                "DRINKING_WATER" => "WATER",
                "READY_TO_EAT_FOOD" or "CHILD_SUPPLIES" => "FOOD",
                "DRY_CLOTHES" => "CLOTHES",
                "BLANKET" => "BLANKET",
                "MEDICINE" => "MEDICINE",
                _ => "OTHER"
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var needsWater = mobileSupplies.Contains("WATER", StringComparer.Ordinal);
        var needsFood = mobileSupplies.Contains("FOOD", StringComparer.Ordinal);
        var needsBlanket = mobileSupplies.Contains("BLANKET", StringComparer.Ordinal);
        var needsMedicine = mobileSupplies.Contains("MEDICINE", StringComparer.Ordinal)
            || scenario.Victims.Any(victim => HueStadiumMedicalIssuesForVictim(victim).Count > 0);
        var needsClothing = mobileSupplies.Contains("CLOTHES", StringComparer.Ordinal)
            || scenario.Victims.Any(victim => HueStadiumNeedsClothing(victim));
        // MedicineCondition enum: HIGH_FEVER, CHRONIC_DISEASE, INJURED, OTHER
        var medicineConditions = scenario.Victims
            .SelectMany(v =>
            {
                var issues = HueStadiumMedicalIssuesForVictim(v);
                var conditions = new List<string>();
                if (issues.Contains("HIGH_FEVER")) conditions.Add("HIGH_FEVER");
                if (issues.Contains("CHRONIC_DISEASE") || issues.Contains("CHEST_PAIN_STROKE")) conditions.Add("CHRONIC_DISEASE");
                if (issues.Any(i => i is "BLEEDING" or "FRACTURE" or "BURNS" or "HEAD_INJURY")) conditions.Add("INJURED");
                if (issues.Contains("BREATHING_DIFFICULTY")) conditions.Add("OTHER");
                return conditions;
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        // MedicalSupportNeed enum: COMMON_MEDICINE, FIRST_AID, CHRONIC_MAINTENANCE, MINOR_INJURY
        var medicalNeeds = scenario.Victims
            .SelectMany(v =>
            {
                var issues = HueStadiumMedicalIssuesForVictim(v);
                var needs = new List<string>();
                if (issues.Contains("HIGH_FEVER")) needs.Add("COMMON_MEDICINE");
                if (issues.Contains("CHRONIC_DISEASE") || issues.Contains("CHEST_PAIN_STROKE")) needs.Add("CHRONIC_MAINTENANCE");
                if (issues.Any(i => i is "BLEEDING" or "FRACTURE" or "BURNS" or "HEAD_INJURY")) needs.Add("FIRST_AID");
                if (issues.Contains("BREATHING_DIFFICULTY")) needs.Add("MINOR_INJURY");
                return needs;
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var hasOther = mobileSupplies.Contains("OTHER", StringComparer.Ordinal);

        return new
        {
            supplies = mobileSupplies,
            water = needsWater
                ? new { duration = "12_TO_24H" }
                : null,
            food = needsFood
                ? new { duration = "12_TO_24H" }
                : null,
            blanket = needsBlanket
                ? new { is_cold_or_wet = true, availability = "NOT_ENOUGH", request_count = Math.Max(1, Math.Min(totalPeople, 4)) }
                : null,
            medicine = needsMedicine
                ? new
                {
                    needs_urgent_medicine = scenario.NeedMedical,
                    conditions = medicineConditions.Count > 0 ? medicineConditions : new List<string> { "OTHER" },
                    medical_needs = medicalNeeds.Count > 0 ? medicalNeeds : new List<string> { "COMMON_MEDICINE" },
                    medical_description = scenario.AdditionalDescription
                }
                : null,
            clothing = needsClothing
                ? new { status = "PARTIALLY_LACKING", gender = (string?)null }
                : null,
            other_supply_description = hasOther ? scenario.AdditionalDescription : (string?)null
        };
    }

    private static object BuildHueStadiumVictimIncidentStatus(HueStadiumVictimScenario victim)
    {
        var medicalIssues = HueStadiumMedicalIssuesForVictim(victim);
        var isInjured = IsHueStadiumVictimInjured(victim, medicalIssues);
        var severity = HueStadiumVictimSeverity(victim, isInjured);

        return new Dictionary<string, object?>
        {
            ["is_injured"] = isInjured,
            ["medical_issues"] = medicalIssues,
            ["severity"] = severity
        };
    }

    private static object BuildHueStadiumVictimPersonalNeeds(HueStadiumVictimScenario victim)
    {
        var hasSpecialDiet = victim.PersonalNeeds.Any(need => need is "LOW_SALT_FOOD" or "DIABETES_MEDICINE" or "MILK" or "PORRIDGE");
        // ClothingGender mobile enum: MALE | FEMALE | null (no CHILD)
        var clothingGender = (string?)null;

        return new
        {
            clothing = new
            {
                needed = HueStadiumNeedsClothing(victim),
                gender = clothingGender
            },
            diet = new
            {
                has_special_diet = hasSpecialDiet,
                description = hasSpecialDiet ? HueStadiumDietDescription(victim) : null
            }
        };
    }

    private static (int Adult, int Child, int Elderly) CountHueStadiumPeople(IReadOnlyList<HueStadiumVictimScenario> victims)
    {
        var child = victims.Count(victim => string.Equals(victim.PersonType, "CHILD", StringComparison.Ordinal));
        var elderly = victims.Count(victim => string.Equals(victim.PersonType, "ELDERLY", StringComparison.Ordinal));
        var adult = victims.Count - child - elderly;
        return (Math.Max(0, adult), child, elderly);
    }

    private static bool IsHueStadiumVictimInjured(HueStadiumVictimScenario victim, IReadOnlyCollection<string> medicalIssues)
        => medicalIssues.Count > 0
            || victim.IncidentStatus is "INJURED" or "CRITICAL" or "MODERATE";

    private static bool HueStadiumNeedsClothing(HueStadiumVictimScenario victim)
        => victim.PersonalNeeds.Any(need => need is "DRY_CLOTHES" or "HYPOTHERMIA_BLANKET" or "BLANKET");

    private static string? HueStadiumVictimSeverity(HueStadiumVictimScenario victim, bool isInjured)
    {
        if (!isInjured)
        {
            return null;
        }

        return victim.IncidentStatus switch
        {
            "CRITICAL" => "CRITICAL",
            "INJURED" or "MODERATE" or "AT_RISK" => "HIGH",
            _ => null
        };
    }

    private static List<string> HueStadiumMedicalIssuesForVictim(HueStadiumVictimScenario victim)
    {
        // MedicalIssue mobile whitelist: BLEEDING, SEVERELY_BLEEDING, FRACTURE, HEAD_INJURY, BURNS,
        // UNCONSCIOUS, BREATHING_DIFFICULTY, CHEST_PAIN_STROKE, CANNOT_MOVE, DROWNING,
        // HIGH_FEVER, DEHYDRATION, INFANT_NEEDS_MILK, LOST_PARENT, CHRONIC_DISEASE,
        // CONFUSION, NEEDS_MEDICAL_DEVICE, OTHER
        var needs = victim.PersonalNeeds;
        var issues = new SortedSet<string>(StringComparer.Ordinal);

        if (needs.Any(need => need.Contains("FRACTURE", StringComparison.Ordinal)))
        {
            issues.Add("FRACTURE");
        }

        if (needs.Any(need => need.Contains("HEART", StringComparison.Ordinal)))
        {
            issues.Add("CHEST_PAIN_STROKE");
        }

        if (needs.Any(need => need.Contains("DIABETES", StringComparison.Ordinal)))
        {
            issues.Add("CHRONIC_DISEASE");
        }

        if (needs.Any(need => need.Contains("FEVER", StringComparison.Ordinal)))
        {
            issues.Add("HIGH_FEVER");
        }

        if (needs.Any(need => need.Contains("OXYGEN", StringComparison.Ordinal)))
        {
            issues.Add("BREATHING_DIFFICULTY");
        }

        if (needs.Any(need => need.Contains("BLOOD_PRESSURE", StringComparison.Ordinal)))
        {
            issues.Add("CHRONIC_DISEASE");
        }

        if (needs.Any(need => need.Contains("WOUND", StringComparison.Ordinal)
                || need.Contains("FIRST_AID", StringComparison.Ordinal)
                || need.Contains("PAIN", StringComparison.Ordinal))
            || victim.IncidentStatus == "INJURED")
        {
            issues.Add("BLEEDING");
        }

        return issues.ToList();
    }

    private static string HueStadiumSosTypeLabel(string sosType) => sosType switch
    {
        "Relief" => "[CỨU TRỢ]",
        "Both" => "[CỨU HỘ + CỨU TRỢ]",
        _ => "[CỨU HỘ]"
    };

    private static string HueStadiumSituationLabel(string situation) => situation switch
    {
        "TRAPPED" => "Mắc kẹt",
        "COLLAPSED" => "Sụp đổ / Đổ vỡ",
        "DANGER_ZONE" => "Vùng nguy hiểm",
        "CANNOT_MOVE" => "Không thể di chuyển",
        "FLOODING" => "Ngập lụt",
        "OTHER" => "Tình huống khác",
        _ => situation
    };

    private static string HueStadiumPersonTypeLabel(string personType) => personType switch
    {
        "CHILD" => "Trẻ em",
        "ELDERLY" => "Người già",
        _ => "Người lớn"
    };

    private static string HueStadiumMedicalIssueLabel(string? issue) => issue switch
    {
        "FRACTURE" => "Gãy xương",
        "BLEEDING" => "Chảy máu",
        "SEVERELY_BLEEDING" => "Chảy máu nặng",
        "HEAD_INJURY" => "Chấn thương đầu",
        "BURNS" => "Bỏng",
        "UNCONSCIOUS" => "Bất tỉnh",
        "CHEST_PAIN_STROKE" => "Đau ngực / Đột quỵ",
        "CANNOT_MOVE" => "Không thể di chuyển",
        "DROWNING" => "Đuối nước",
        "HIGH_FEVER" => "Sốt cao",
        "DEHYDRATION" => "Mất nước",
        "CHRONIC_DISEASE" => "Bệnh nền",
        "BREATHING_DIFFICULTY" => "Khó thở",
        "NEEDS_MEDICAL_DEVICE" => "Cần thiết bị y tế",
        "CONFUSION" => "Lú lẫn",
        _ => "Cần hỗ trợ y tế"
    };

    private static string HueStadiumDietDescription(HueStadiumVictimScenario victim)
    {
        if (victim.PersonalNeeds.Contains("LOW_SALT_FOOD", StringComparer.Ordinal))
        {
            return "Ăn nhạt, hạn chế muối.";
        }

        if (victim.PersonalNeeds.Contains("DIABETES_MEDICINE", StringComparer.Ordinal))
        {
            return "Cần kiểm soát đường huyết và ăn đúng bữa.";
        }

        if (victim.PersonalNeeds.Contains("MILK", StringComparer.Ordinal))
        {
            return "Cần sữa hoặc thức ăn mềm cho trẻ nhỏ.";
        }

        return "Có nhu cầu ăn uống riêng.";
    }

    private static string BuildHueStadiumRuleItemsNeeded(SosRequest sos)
    {
        // SupplyNeed mobile enum: WATER | FOOD | CLOTHES | BLANKET | MEDICINE | OTHER
        var items = new SortedSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(sos.StructuredData))
        {
            using var document = JsonDocument.Parse(sos.StructuredData);
            if (document.RootElement.TryGetProperty("group_needs", out var groupNeeds)
                && groupNeeds.ValueKind == JsonValueKind.Object
                && groupNeeds.TryGetProperty("supplies", out var supplies)
                && supplies.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in supplies.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        items.Add(value);
                }
            }

            if (document.RootElement.TryGetProperty("incident", out var incident))
            {
                if (incident.TryGetProperty("need_medical", out var needMedical) && needMedical.GetBoolean())
                    items.Add("MEDICINE");

                if (incident.TryGetProperty("has_injured", out var hasInjured) && hasInjured.GetBoolean())
                    items.Add("MEDICINE");
            }
        }

        if (sos.SosType is "Relief" or "Both")
        {
            items.Add("WATER");
            items.Add("FOOD");
        }

        return Json(items);
    }

    private static string SosAddressFromStructuredData(string? structuredData)
    {
        if (string.IsNullOrWhiteSpace(structuredData))
        {
            return "Khu dân cư quanh Sân vận động Tự Do";
        }

        using var document = JsonDocument.Parse(structuredData);
        if (document.RootElement.TryGetProperty("incident", out var incident)
            && incident.TryGetProperty("address", out var nestedAddress))
        {
            return nestedAddress.GetString() ?? "Khu dân cư quanh Sân vận động Tự Do";
        }

        if (document.RootElement.TryGetProperty("address", out var address))
        {
            return address.GetString() ?? "Khu dân cư quanh Sân vận động Tự Do";
        }

        return "Khu dân cư quanh Sân vận động Tự Do";
    }

    private static (
        IReadOnlyList<HueStadiumClusterScenario> Clusters,
        IReadOnlyList<HueStadiumSosScenario> SosRequests)
        CreateHueStadiumSosScenarios()
    {
        var clusters = new[]
        {
            new HueStadiumClusterScenario("HUE-TD-01", 16.462120, 107.602860, 0.34, "High", "Ngập 0.8m quanh cổng", 9, 2, 1, 0.72, "Pending", new DateTime(2026, 4, 24, 6, 45, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-02", 16.461170, 107.606060, 0.18, "Critical", "Ngập 1.2m phía đông", 4, 0, 0, 0.91, "InProgress", new DateTime(2026, 4, 24, 7, 5, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-03", 16.458620, 107.601700, 0.24, "High", "Nước rút còn 0.4m", 7, 1, 1, 0.61, "Completed", new DateTime(2026, 4, 24, 7, 40, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-04", 16.462630, 107.603940, 0.42, "High", "Ngập kiệt nhỏ, có điểm dây điện võng thấp", 9, 0, 1, 0.70, "Pending", new DateTime(2026, 4, 24, 8, 15, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-05", 16.459900, 107.604470, 0.24, "High", "Nước chảy xiết", 8, 1, 1, 0.79, "InProgress", new DateTime(2026, 4, 24, 9, 0, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-06", 16.465500, 107.603400, 0.22, "Medium", "Ngập cục bộ", 5, 1, 1, 0.42, "Completed", new DateTime(2026, 4, 24, 9, 35, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-07", 16.457760, 107.606050, 0.16, "Critical", "Ngập sâu 1.4m", 5, 0, 0, 0.88, "InProgress", new DateTime(2026, 4, 24, 10, 20, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-08", 16.466740, 107.598890, 0.20, "Low", "Ngập rải rác", 2, 0, 0, 0.26, "InProgress", new DateTime(2026, 4, 24, 11, 5, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-09", 16.462260, 107.602950, 0.38, "Medium", "Ngập quanh các kiệt nhỏ", 12, 2, 1, 0.55, "InProgress", new DateTime(2026, 4, 24, 11, 30, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-10", 16.460740, 107.606690, 0.16, "Critical", "Ngập sâu phía đông", 6, 1, 1, 0.90, "InProgress", new DateTime(2026, 4, 24, 11, 55, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-11", 16.458300, 107.606000, 0.22, "High", "Ngập sâu ở cổng phụ phía nam", 8, 1, 1, 0.76, "InProgress", new DateTime(2026, 4, 24, 12, 20, 0, DateTimeKind.Unspecified))
        };

        var sosRequests = new[]
        {
            new HueStadiumSosScenario(0, 16.458574, 107.572864, "50 Bùi Thị Xuân, Thuận Hóa, Huế, Việt Nam", "Rescue", "Pending", "High", 74, "TRAPPED", false, true, true, false, "Ba người kẹt ở tầng trệt, nước dâng nhanh và có một người bị rách chân. Tôi đang ở Đảo huế, 50 Bùi Thị Xuân, Thuận Hóa, Huế để đội cứu hộ dễ nhận diện.", "Gia đình tôi ở Đảo huế, 50 Bùi Thị Xuân, nước vào nhà gần tới thắt lưng. Có 3 người, một người bị rách chân, cần xuồng tiếp cận gấp.", "4G", 34, false, 2, 2, 0, new DateTime(2026, 4, 24, 7, 12, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Minh", "TRAPPED", ["FIRST_AID", "EVACUATION_SUPPORT"]), new HueStadiumVictimScenario("mother", "ELDERLY", "Bà Lan", "INJURED", ["WHEELCHAIR_SUPPORT", "BLOOD_PRESSURE_MEDICINE"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Nam", "SCARED", ["CHILD_LIFE_JACKET"])], []),
            new HueStadiumSosScenario(0, 16.475395, 107.593210, "121 kiệt 7 Ưng Bình tổ 16, Vỹ Dạ, Huế, Việt Nam", "Both", "Pending", "High", 70, "FLOODING", false, false, false, true, "Nhóm bốn người mắc kẹt trên gác, còn nước uống khoảng nửa ngày. Tôi đang ở khu vực 121 kiệt 7 Ưng Bình tổ 16, Vỹ Dạ, Huế để đội cứu hộ dễ nhận diện.", "Nhà tại 121 kiệt 7 Ưng Bình tổ 16, Vỹ Dạ bị ngập, bốn người đang ở trên gác. Cần đội cứu hộ kiểm tra và mang nước uống, đồ ăn khô.", "WIFI", 58, false, 3, 4, 1, new DateTime(2026, 4, 24, 7, 28, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Chị Hạnh", "TRAPPED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("father", "ELDERLY", "Ông Phú", "STABLE", ["LOW_SALT_FOOD"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé My", "STABLE", ["MILK"]), new HueStadiumVictimScenario("child-2", "CHILD", "Bé Bo", "STABLE", ["CHILD_MEDICINE"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD", "CHILD_SUPPLIES"]),
            new HueStadiumSosScenario(8, 16.469991, 107.577532, "Hoàng Thành Huế", "Relief", "Pending", "Medium", 57, "OTHER", true, false, false, true, "Điểm trú tạm thiếu nước sạch, pin sạc và chăn cho trẻ nhỏ. Tôi đang ở Hoàng Thành Huế để đội cứu hộ dễ nhận diện.", "Chúng tôi đã lên tầng hai an toàn nhưng có 6 người trú tạm trong khu vực Hoàng Thành Huế, thiếu nước sạch, chăn và pin sạc điện thoại từ sáng.", "MESH", 22, false, 4, 4, 2, new DateTime(2026, 4, 24, 7, 51, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("group-lead", "ADULT", "Cô Thảo", "SAFE", ["POWER_BANK"]), new HueStadiumVictimScenario("older-neighbor", "ELDERLY", "Bác Năm", "STABLE", ["BLANKET"])], ["DRINKING_WATER", "BLANKET", "OTHER"]),
            new HueStadiumSosScenario(1, 16.458840, 107.579282, "Sân pickle ball Đại học Huế", "Rescue", "Assigned", "Critical", 91, "TRAPPED", false, true, true, false, "Một người lớn bị gãy tay nghi ngờ, nhóm đang bám lan can trước nhà. Tôi đang ở Sân pickle ball Đại học Huế để đội cứu hộ dễ nhận diện.", "Nhà tôi ở gần Sân pickle ball Đại học Huế, nước chảy mạnh. Có người nghi gãy tay, không thể tự ra ngoài, xin đội cứu hộ đến ngay.", "5G", 46, false, 5, 5, 3, new DateTime(2026, 4, 24, 8, 6, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Dũng", "INJURED", ["FRACTURE_SPLINT", "PAIN_RELIEF"]), new HueStadiumVictimScenario("wife", "ADULT", "Chị Mai", "TRAPPED", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(9, 16.487637373515994, 107.59085080106323, "Đ. Chi Lăng, Phú Hiệp, Phú Xuân, Huế, Việt Nam", "Both", "InProgress", "Critical", 88, "OTHER", false, true, true, false, "Cụ ông khó thở sau khi ngâm nước lâu, cần sơ cứu và áo phao để đưa ra. Tôi đang ở 365 chị Hạnh Bánh cuốn - bún mắm nêm, Đ. Chi Lăng, Phú Hiệp, Phú Xuân, Huế để đội cứu hộ dễ nhận diện.", "Cụ ông 78 tuổi khó thở, gia đình 5 người bị kẹt gần 365 chị Hạnh Bánh cuốn - bún mắm nêm trên đường Chi Lăng. Cần y tế và áo phao trẻ em.", "4G", 41, true, 6, 7, 4, new DateTime(2026, 4, 24, 8, 34, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("grandfather", "ELDERLY", "Ông Tịnh", "CRITICAL", ["OXYGEN_CHECK", "HEART_MEDICINE"]), new HueStadiumVictimScenario("adult-1", "ADULT", "Chị Ngọc", "TRAPPED", ["EVACUATION_SUPPORT"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé Su", "STABLE", ["CHILD_LIFE_JACKET"])], ["OTHER", "DRINKING_WATER", "MEDICINE"]),
            new HueStadiumSosScenario(3, 16.46273510136702, 107.59891596386217, "10 Đ. Nguyễn Lương Bằng, tổ 3, Thuận Hóa, Huế, Việt Nam", "Rescue", "Incident", "High", 76, "DANGER_ZONE", true, false, false, true, "Đường vào bị dây điện võng thấp, cần đội kiểm tra trước khi sơ tán. Tôi đang ở Cà phê muối, 10 Đ. Nguyễn Lương Bằng, tổ 3, Thuận Hóa, Huế để đội cứu hộ dễ nhận diện.", "Lối vào khu Cà phê muối, 10 Đ. Nguyễn Lương Bằng có dây điện võng xuống nước. Gia đình còn trong nhà, chưa dám di chuyển.", "4G", 63, false, 8, 8, 0, new DateTime(2026, 4, 24, 8, 58, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Khánh", "AT_RISK", ["ROUTE_CLEARANCE"]), new HueStadiumVictimScenario("mother", "ELDERLY", "Mẹ Khánh", "STABLE", ["ESCORT_SUPPORT"])], []),
            new HueStadiumSosScenario(2, 16.458740, 107.601460, "Kiệt 5 Nguyễn Huệ nối về Sân Tự Do, Huế", "Rescue", "Resolved", "High", 69, "OTHER", true, true, true, true, "Hai người đã được đưa ra khỏi vùng ngập, còn cần ghi nhận y tế sau sơ cứu.", "Hai người trong kiệt 5 Nguyễn Huệ đã được đội xuồng đưa ra, một người trầy chân đã băng bó tạm.", "4G", 77, false, 9, 9, 1, new DateTime(2026, 4, 24, 9, 18, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Chị Duyên", "RESCUED", ["WOUND_CLEANING"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Linh", "RESCUED", ["DRY_CLOTHES"])], []),
            new HueStadiumSosScenario(2, 16.458420, 107.601940, "Nhà trọ sau Sân Tự Do, gần Nguyễn Huệ, Huế", "Both", "Resolved", "Medium", 52, "OTHER", true, false, false, true, "Nhóm sinh viên đã nhận nước và được hướng dẫn ra điểm tập kết.", "Nhóm sinh viên ở nhà trọ sau sân Tự Do thiếu nước và mì, đã được đội hỗ trợ chuyển đến điểm tập kết.", "WIFI", 69, false, 10, 10, 2, new DateTime(2026, 4, 24, 9, 31, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("group-lead", "ADULT", "Bạn Hoàng", "RESCUED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("roommate", "ADULT", "Bạn Phúc", "RESCUED", ["READY_TO_EAT_FOOD"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(8, 16.470787, 107.606622, "147/2 Nguyễn Lộ Trạch, Xuân Phú, Vỹ Dạ, Huế, Việt Nam", "Relief", "Pending", "Medium", 49, "OTHER", true, false, false, true, "Một điểm trú tạm 7 người cần nước, cháo ăn liền và thuốc hạ sốt. Tôi đang ở quán Bún bò bà Bê, 147/2 Nguyễn Lộ Trạch, Xuân Phú, Vỹ Dạ, Huế để đội cứu hộ dễ nhận diện.", "Điểm trú ở quán Bún bò bà Bê, 147/2 Nguyễn Lộ Trạch có 7 người, trong đó có trẻ nhỏ. Cần nước sạch, cháo ăn liền, thuốc hạ sốt.", "5G", 52, false, 11, 12, 3, new DateTime(2026, 4, 24, 10, 3, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Cô Lệ", "SAFE", ["FEVER_MEDICINE"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé Bảo", "STABLE", ["PORRIDGE", "MILK"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD", "MEDICINE"]),
            new HueStadiumSosScenario(3, 16.457306, 107.600528, "Phú Nhuận, Thuận Hóa, Huế, Việt Nam", "Rescue", "Pending", "High", 72, "TRAPPED", false, false, false, true, "Hai người bị kẹt, cửa cuốn hỏng do mất điện. Tôi đang ở Chợ An Cựu, Phú Nhuận, Thuận Hóa, Huế để đội cứu hộ dễ nhận diện.", "Hai người đang kẹt gần Chợ An Cựu, Phú Nhuận, cửa cuốn không mở vì mất điện, nước ngoài đường lên nhanh.", "MESH", 28, false, 12, 12, 4, new DateTime(2026, 4, 24, 10, 22, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("owner", "ADULT", "Anh Sơn", "TRAPPED", ["DOOR_OPENING_SUPPORT"]), new HueStadiumVictimScenario("staff", "ADULT", "Bạn Vy", "TRAPPED", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(4, 16.56125867722612, 107.64617672877375, "84 Hoàng Sa, Hải Tiến, Thuận An, Huế 49918, Việt Nam", "Both", "Assigned", "High", 73, "FLOODING", false, true, true, false, "Nhà có phụ nữ mang thai đau bụng nhẹ, nhóm cần được đưa ra và nhận nước sạch. Tôi đang ở Sea to Sea Villa, 84 Hoàng Sa, Hải Tiến, Thuận An, Huế để đội cứu hộ dễ nhận diện.", "Phụ nữ mang thai tại Sea to Sea Villa, 84 Hoàng Sa đau bụng nhẹ, nước ngập qua đầu gối và có trẻ nhỏ. Cần đội đưa ra điểm an toàn, mang thêm nước sạch.", "4G", 49, true, 13, 14, 0, new DateTime(2026, 4, 24, 10, 49, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("pregnant", "ADULT", "Chị Hà (mang thai)", "MODERATE", ["FIRST_AID"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Sóc", "STABLE", ["CHILD_LIFE_JACKET"])], ["DRINKING_WATER", "OTHER"]),
            new HueStadiumSosScenario(8, 16.564993, 107.638082, "190 Hoàng Sa, Hải Tiến, Thuận An, Huế, Việt Nam", "Relief", "InProgress", "Medium", 58, "OTHER", true, false, false, true, "Năm người ở tầng hai thiếu thuốc tiểu đường và nước sạch. Tôi đang ở Sân Bóng Chuyền Hải Tiến, 190 Hoàng Sa, Hải Tiến, Thuận An, Huế để đội cứu hộ dễ nhận diện.", "Gia đình 5 người ở gần Sân Bóng Chuyền Hải Tiến, 190 Hoàng Sa đã lên tầng hai, đang thiếu nước uống và thuốc tiểu đường cho người lớn tuổi.", "WIFI", 71, false, 14, 14, 1, new DateTime(2026, 4, 24, 11, 18, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("father", "ELDERLY", "Ông Bảy", "STABLE", ["DIABETES_MEDICINE"]), new HueStadiumVictimScenario("adult", "ADULT", "Chị Loan", "SAFE", ["DRINKING_WATER"])], ["DRINKING_WATER", "MEDICINE", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(4, 16.482238, 107.594262, "12 Ưng Bình, Vỹ Dạ, Huế, Việt Nam", "Rescue", "Pending", "High", 68, "DANGER_ZONE", false, false, false, true, "Cầu thang ngoài bị ngập, người già không thể xuống tầng trệt. Tôi đang ở khu vực 12 Ưng Bình, Vỹ Dạ, Huế để đội cứu hộ dễ nhận diện.", "Người già trong nhà tại 12 Ưng Bình, Vỹ Dạ không thể xuống cầu thang ngoài vì nước xiết. Cần đội có dây hỗ trợ tiếp cận.", "4G", 37, false, 15, 16, 2, new DateTime(2026, 4, 24, 11, 46, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("grandmother", "ELDERLY", "Bà Cúc", "TRAPPED", ["ROPE_ASSIST", "ESCORT_SUPPORT"]), new HueStadiumVictimScenario("adult", "ADULT", "Anh Tú", "AT_RISK", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(5, 16.465260, 107.603120, "Đường Trần Cao Vân gần lối vào Sân Tự Do, Huế", "Relief", "Resolved", "Medium", 44, "OTHER", true, false, false, true, "Điểm trú đã nhận nước và mì, không còn yêu cầu mở.", "Điểm trú trên Trần Cao Vân đã nhận nước và mì từ đội hỗ trợ, mọi người an toàn.", "5G", 80, false, 16, 16, 3, new DateTime(2026, 4, 24, 12, 9, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult", "ADULT", "Chú Lộc", "RESCUED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("elderly", "ELDERLY", "Bà Tâm", "RESCUED", ["BLANKET"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(5, 16.465720, 107.603640, "Sau khu nhà thi đấu phụ Sân Tự Do, Huế", "Rescue", "Resolved", "Medium", 47, "OTHER", true, false, false, true, "Hai người đã tự ra theo hướng dẫn của đội, không cần thêm cứu hộ.", "Hai người ở sau nhà thi đấu phụ đã được hướng dẫn ra đường cao hơn, hiện an toàn.", "4G", 66, false, 17, 17, 4, new DateTime(2026, 4, 24, 12, 27, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Quốc", "RESCUED", ["CHECK_IN"]), new HueStadiumVictimScenario("wife", "ADULT", "Chị Nhi", "RESCUED", ["CHECK_IN"])], []),
            new HueStadiumSosScenario(6, 16.457760, 107.606050, "Đoạn Lê Quý Đôn gần cổng phụ Sân Tự Do, Huế", "Both", "Incident", "Critical", 92, "OTHER", false, true, true, false, "Một người bị hạ thân nhiệt, đội báo cần cáng mềm và áo giữ nhiệt.", "Có người ngâm nước lâu bị lạnh run, lơ mơ. Đội đang tiếp cận nhưng cần thêm cáng mềm, áo giữ nhiệt và nước ấm.", "4G", 44, false, 18, 18, 0, new DateTime(2026, 4, 24, 13, 5, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("patient", "ADULT", "Anh Tài", "CRITICAL", ["HYPOTHERMIA_BLANKET", "STRETCHER"]), new HueStadiumVictimScenario("sister", "ADULT", "Chị Trâm", "TRAPPED", ["EVACUATION_SUPPORT"])], ["BLANKET", "MEDICINE", "DRINKING_WATER"]),
            new HueStadiumSosScenario(10, 16.48307194044432, 107.59044430399764, "236 Đ. Chi Lăng, Phú Xuân, Huế, Việt Nam", "Rescue", "InProgress", "High", 78, "TRAPPED", false, false, false, true, "Ba người bị cô lập, điểm đón phù hợp là trước quán. Tôi đang ở Cơm Hến Quỳnh, 236 Đ. Chi Lăng, Phú Xuân, Huế để đội cứu hộ dễ nhận diện.", "Ba người bị cô lập gần Cơm Hến Quỳnh, 236 Đ. Chi Lăng. Nước xoáy ở đầu hẻm, cần xuồng nhỏ tiếp cận.", "MESH", 19, false, 19, 20, 1, new DateTime(2026, 4, 24, 13, 33, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Anh Lâm", "TRAPPED", ["BOAT_RESCUE"]), new HueStadiumVictimScenario("adult-2", "ADULT", "Chị Yến", "TRAPPED", ["BOAT_RESCUE"]), new HueStadiumVictimScenario("elderly", "ELDERLY", "Ông Hòa", "TRAPPED", ["ESCORT_SUPPORT"])], []),
            new HueStadiumSosScenario(7, 16.466530, 107.598650, "Đường Đống Đa hướng về Sân Tự Do, Huế", "Relief", "Resolved", "Low", 33, "OTHER", true, false, false, true, "Yêu cầu nước sạch đã được nhóm địa phương xử lý.", "Khu Đống Đa đã nhận nước sạch từ nhóm địa phương, cập nhật để đóng yêu cầu.", "5G", 83, false, 20, 20, 2, new DateTime(2026, 4, 24, 14, 2, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult", "ADULT", "Cô Vân", "RESCUED", ["DRINKING_WATER"])], ["DRINKING_WATER"]),
            new HueStadiumSosScenario(7, 16.466940, 107.599120, "Kiệt 44 Đống Đa, cách Sân Tự Do 600m, Huế", "Rescue", "Cancelled", "Low", 26, "OTHER", true, false, false, true, "Người gửi báo đã tự di chuyển ra khỏi khu ngập, không cần đội đến.", "Tôi đã tự ra khỏi kiệt 44 Đống Đa nhờ hàng xóm hỗ trợ, xin hủy yêu cầu cứu hộ.", "WIFI", 61, false, 21, 21, 3, new DateTime(2026, 4, 24, 14, 26, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Huy", "SAFE", ["CHECK_IN"])], []),
            new HueStadiumSosScenario(10, 16.471497, 107.597261, "59 Nguyễn Công Trứ, Phú Hội, Thuận Hóa, Huế, Việt Nam", "Both", "InProgress", "Medium", 63, "FLOODING", false, false, false, true, "Bốn người chờ đội ở tầng hai, cần nước uống và đèn pin vì mất điện. Tôi đang ở quán Bún Thit Nướng, 59 Nguyễn Công Trứ, Phú Hội, Thuận Hóa, Huế để đội cứu hộ dễ nhận diện.", "Bốn người đang chờ ở tầng hai tại quán Bún Thit Nướng, 59 Nguyễn Công Trứ. Nhà mất điện, cần nước uống và đèn pin khi đội tới.", "4G", 39, true, 22, 23, 4, new DateTime(2026, 4, 24, 14, 51, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Chị Nương", "TRAPPED", ["FLASHLIGHT"]), new HueStadiumVictimScenario("adult-2", "ADULT", "Anh Bình", "TRAPPED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Kem", "STABLE", ["CHILD_LIFE_JACKET"])], ["DRINKING_WATER", "OTHER"])
        };

        return (clusters, sosRequests);
    }


    private static string SosUpdateContent(string? status)
    {
        return status switch
        {
            "Resolved" => "Đội cứu hộ xác nhận đã hỗ trợ xong và cập nhật an toàn.",
            "InProgress" => "Đội cứu hộ đang trên đường, ETA khoảng 20 phút.",
            "Assigned" => "Đã phân công đội phụ trách tiếp cận hiện trường.",
            "Cancelled" => "Yêu cầu đã hủy sau khi xác minh an toàn.",
            _ => "Đang chờ điều phối viên xác nhận thêm thông tin."
        };
    }
}
