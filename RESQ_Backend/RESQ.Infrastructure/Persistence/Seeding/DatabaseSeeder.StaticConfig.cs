using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private async Task SeedStaticConfigAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        if (!await _db.AbilityCategories.AnyAsync(cancellationToken))
        {
            var categories = new[]
            {
                new AbilityCategory { Id = 1, Code = "RESCUE", Description = "Kỹ năng cứu hộ" },
                new AbilityCategory { Id = 2, Code = "MEDICAL", Description = "Kỹ năng y tế" },
                new AbilityCategory { Id = 3, Code = "TRANSPORTATION", Description = "Kỹ năng vận chuyển" },
                new AbilityCategory { Id = 4, Code = "EXPERIENCE", Description = "Kinh nghiệm thực tiễn" }
            };

            var subgroups = new[]
            {
                // RESCUE
                new AbilitySubgroup { Id = 1, Code = "WATER_SKILLS", Description = "Kỹ năng bơi lội", AbilityCategoryId = 1 },
                new AbilitySubgroup { Id = 2, Code = "LIFESAVING_SKILLS", Description = "Kỹ năng cứu người", AbilityCategoryId = 1 },
                new AbilitySubgroup { Id = 3, Code = "HARSH_ENVIRONMENT_RESCUE", Description = "Cứu hộ trong điều kiện khắc nghiệt", AbilityCategoryId = 1 },
                // MEDICAL
                new AbilitySubgroup { Id = 4, Code = "PROFESSIONAL_MEDICAL", Description = "Y tế chuyên môn", AbilityCategoryId = 2 },
                new AbilitySubgroup { Id = 5, Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản", AbilityCategoryId = 2 },
                new AbilitySubgroup { Id = 6, Code = "EMERGENCY_CARE", Description = "Cấp cứu", AbilityCategoryId = 2 },
                new AbilitySubgroup { Id = 7, Code = "TRAUMA_CARE", Description = "Chấn thương", AbilityCategoryId = 2 },
                // TRANSPORTATION
                new AbilitySubgroup { Id = 8, Code = "LAND_VEHICLES", Description = "Lái xe cơ giới", AbilityCategoryId = 3 },
                new AbilitySubgroup { Id = 9, Code = "WATER_VEHICLES", Description = "Lái phương tiện thủy", AbilityCategoryId = 3 },
                new AbilitySubgroup { Id = 10, Code = "SPECIALIZED_DRIVING", Description = "Kỹ năng điều khiển đặc biệt", AbilityCategoryId = 3 },
                new AbilitySubgroup { Id = 11, Code = "TRANSPORT_OPERATIONS", Description = "Vận chuyển", AbilityCategoryId = 3 },
                // EXPERIENCE
                new AbilitySubgroup { Id = 12, Code = "FIELD_EXPERIENCE", Description = "Kinh nghiệm thực tế", AbilityCategoryId = 4 },
                new AbilitySubgroup { Id = 13, Code = "ORGANIZATIONAL_MEMBERSHIP", Description = "Tổ chức", AbilityCategoryId = 4 }
            };

            var abilities = new[]
            {
                // WATER_SKILLS (subgroup 1)
                new Ability { Id = 1, Code = "BASIC_SWIMMING", Description = "Bơi cơ bản", AbilitySubgroupId = 1 },
                new Ability { Id = 2, Code = "ADVANCED_SWIMMING", Description = "Bơi thành thạo", AbilitySubgroupId = 1 },
                new Ability { Id = 3, Code = "WATER_RESCUE", Description = "Cứu hộ dưới nước", AbilitySubgroupId = 1 },
                new Ability { Id = 4, Code = "DEEP_WATER_MOVEMENT", Description = "Di chuyển trong nước ngập sâu", AbilitySubgroupId = 1 },
                new Ability { Id = 5, Code = "RAPID_WATER_MOVEMENT", Description = "Di chuyển trong dòng nước chảy xiết", AbilitySubgroupId = 1 },
                new Ability { Id = 6, Code = "BASIC_DIVING", Description = "Lặn cơ bản", AbilitySubgroupId = 1 },
                new Ability { Id = 7, Code = "FLOOD_ESCAPE", Description = "Thoát hiểm trong môi trường ngập nước", AbilitySubgroupId = 1 },
                // LIFESAVING_SKILLS (subgroup 2)
                new Ability { Id = 8, Code = "FLOODED_HOUSE_RESCUE", Description = "Cứu người bị mắc kẹt trong nhà ngập", AbilitySubgroupId = 2 },
                new Ability { Id = 9, Code = "ROOFTOP_RESCUE", Description = "Cứu người bị mắc kẹt trên mái nhà", AbilitySubgroupId = 2 },
                new Ability { Id = 10, Code = "VEHICLE_RESCUE", Description = "Cứu người bị kẹt trong phương tiện (xe, ghe)", AbilitySubgroupId = 2 },
                new Ability { Id = 11, Code = "ROPE_RESCUE", Description = "Sử dụng dây thừng cứu hộ", AbilitySubgroupId = 2 },
                new Ability { Id = 12, Code = "LIFE_JACKET_USE", Description = "Sử dụng áo phao, phao cứu sinh", AbilitySubgroupId = 2 },
                // HARSH_ENVIRONMENT_RESCUE (subgroup 3)
                new Ability { Id = 13, Code = "NIGHT_RESCUE", Description = "Cứu hộ ban đêm / tầm nhìn kém", AbilitySubgroupId = 3 },
                new Ability { Id = 14, Code = "STORM_RESCUE", Description = "Cứu hộ trong mưa lớn / bão", AbilitySubgroupId = 3 },
                new Ability { Id = 15, Code = "DEBRIS_RESCUE", Description = "Cứu hộ tại khu vực đổ nát", AbilitySubgroupId = 3 },
                new Ability { Id = 16, Code = "HAZARDOUS_RESCUE", Description = "Cứu hộ trong môi trường nguy hiểm", AbilitySubgroupId = 3 },
                // BASIC_FIRST_AID (subgroup 5)
                new Ability { Id = 17, Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản", AbilitySubgroupId = 5 },
                new Ability { Id = 18, Code = "OPEN_WOUND_CARE", Description = "Sơ cứu vết thương hở", AbilitySubgroupId = 5 },
                new Ability { Id = 19, Code = "BLEEDING_CONTROL", Description = "Cầm máu", AbilitySubgroupId = 5 },
                new Ability { Id = 20, Code = "WOUND_BANDAGING", Description = "Băng bó vết thương", AbilitySubgroupId = 5 },
                new Ability { Id = 21, Code = "MINOR_INJURY_CARE", Description = "Xử lý trầy xước, chấn thương nhẹ", AbilitySubgroupId = 5 },
                new Ability { Id = 22, Code = "MINOR_BURN_CARE", Description = "Xử lý bỏng nhẹ", AbilitySubgroupId = 5 },
                // EMERGENCY_CARE (subgroup 6)
                new Ability { Id = 23, Code = "CPR", Description = "Hồi sức tim phổi (CPR)", AbilitySubgroupId = 6 },
                new Ability { Id = 24, Code = "DROWNING_RESPONSE", Description = "Xử lý đuối nước", AbilitySubgroupId = 6 },
                new Ability { Id = 25, Code = "SHOCK_TREATMENT", Description = "Xử lý sốc", AbilitySubgroupId = 6 },
                new Ability { Id = 26, Code = "HYPOTHERMIA_TREATMENT", Description = "Xử lý hạ thân nhiệt", AbilitySubgroupId = 6 },
                new Ability { Id = 27, Code = "VITAL_SIGNS_MONITORING", Description = "Theo dõi dấu hiệu sinh tồn", AbilitySubgroupId = 6 },
                new Ability { Id = 28, Code = "VICTIM_ASSESSMENT", Description = "Đánh giá mức độ nguy kịch nạn nhân", AbilitySubgroupId = 6 },
                // TRAUMA_CARE (subgroup 7)
                new Ability { Id = 29, Code = "FRACTURE_IMMOBILIZATION", Description = "Cố định gãy xương tạm thời", AbilitySubgroupId = 7 },
                new Ability { Id = 30, Code = "SPINAL_INJURY_CARE", Description = "Xử lý chấn thương cột sống (cơ bản)", AbilitySubgroupId = 7 },
                new Ability { Id = 31, Code = "SAFE_PATIENT_TRANSPORT", Description = "Vận chuyển người bị thương an toàn", AbilitySubgroupId = 7 },
                // PROFESSIONAL_MEDICAL (subgroup 4)
                new Ability { Id = 32, Code = "MEDICAL_STAFF", Description = "Nhân viên y tế", AbilitySubgroupId = 4 },
                new Ability { Id = 33, Code = "NURSE", Description = "Y tá", AbilitySubgroupId = 4 },
                new Ability { Id = 34, Code = "DOCTOR", Description = "Bác sĩ", AbilitySubgroupId = 4 },
                new Ability { Id = 35, Code = "PREHOSPITAL_EMERGENCY", Description = "Cấp cứu tiền viện", AbilitySubgroupId = 4 },
                // LAND_VEHICLES (subgroup 8)
                new Ability { Id = 36, Code = "MOTORCYCLE_DRIVING", Description = "Lái xe máy", AbilitySubgroupId = 8 },
                new Ability { Id = 37, Code = "MOTORCYCLE_FLOOD_DRIVING", Description = "Lái xe máy trong điều kiện ngập nước", AbilitySubgroupId = 8 },
                new Ability { Id = 38, Code = "CAR_DRIVING", Description = "Lái ô tô", AbilitySubgroupId = 8 },
                new Ability { Id = 39, Code = "OFFROAD_DRIVING", Description = "Lái ô tô địa hình", AbilitySubgroupId = 8 },
                // WATER_VEHICLES (subgroup 9)
                new Ability { Id = 40, Code = "ROWBOAT_DRIVING", Description = "Lái ghe", AbilitySubgroupId = 9 },
                new Ability { Id = 41, Code = "DINGHY_DRIVING", Description = "Lái xuồng", AbilitySubgroupId = 9 },
                new Ability { Id = 42, Code = "SPEEDBOAT_DRIVING", Description = "Lái ca nô", AbilitySubgroupId = 9 },
                // SPECIALIZED_DRIVING (subgroup 10)
                new Ability { Id = 43, Code = "NIGHT_VEHICLE_OPERATION", Description = "Điều khiển phương tiện ban đêm", AbilitySubgroupId = 10 },
                new Ability { Id = 44, Code = "RAIN_VEHICLE_OPERATION", Description = "Điều khiển phương tiện trong mưa lớn", AbilitySubgroupId = 10 },
                // TRANSPORT_OPERATIONS (subgroup 11)
                new Ability { Id = 45, Code = "VICTIM_TRANSPORT", Description = "Vận chuyển nạn nhân", AbilitySubgroupId = 11 },
                new Ability { Id = 46, Code = "RELIEF_GOODS_TRANSPORT", Description = "Vận chuyển hàng cứu trợ", AbilitySubgroupId = 11 },
                new Ability { Id = 47, Code = "HEAVY_CARGO_TRANSPORT", Description = "Vận chuyển hàng nặng", AbilitySubgroupId = 11 },
                // FIELD_EXPERIENCE (subgroup 12)
                new Ability { Id = 48, Code = "DISASTER_RELIEF_EXPERIENCE", Description = "Đã tham gia cứu trợ thiên tai", AbilitySubgroupId = 12 },
                new Ability { Id = 49, Code = "FLOOD_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ lũ lụt", AbilitySubgroupId = 12 },
                new Ability { Id = 50, Code = "COMMUNITY_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ cộng đồng", AbilitySubgroupId = 12 },
                // ORGANIZATIONAL_MEMBERSHIP (subgroup 13)
                new Ability { Id = 51, Code = "LOCAL_RESCUE_TEAM_MEMBER", Description = "Thành viên đội cứu hộ địa phương", AbilitySubgroupId = 13 },
                new Ability { Id = 52, Code = "VOLUNTEER_ORG_MEMBER", Description = "Thành viên tổ chức thiện nguyện", AbilitySubgroupId = 13 }
            };

            _db.AbilityCategories.AddRange(categories);
            _db.AbilitySubgroups.AddRange(subgroups);
            _db.Abilities.AddRange(abilities);
        }

        if (!await _db.CheckInRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.CheckInRadiusConfigs.Add(new CheckInRadiusConfig { MaxRadiusMeters = 150, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescueTeamRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.RescueTeamRadiusConfigs.Add(new RescueTeamRadiusConfig { MaxRadiusKm = 10, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescuerScoreVisibilityConfigs.AnyAsync(cancellationToken))
        {
            _db.RescuerScoreVisibilityConfigs.Add(new RescuerScoreVisibilityConfig { MinimumEvaluationCount = 3, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SosClusterGroupingConfigs.AnyAsync(cancellationToken))
        {
            _db.SosClusterGroupingConfigs.Add(new SosClusterGroupingConfig { MaximumDistanceKm = 4.5, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SupplyRequestPriorityConfigs.AnyAsync(cancellationToken))
        {
            _db.SupplyRequestPriorityConfigs.Add(new SupplyRequestPriorityConfig
            {
                UrgentMinutes = 30,
                HighMinutes = 120,
                MediumMinutes = 480,
                UpdatedAt = seed.AnchorUtc
            });
        }

        await SeedServiceZonesAsync(seed.AnchorUtc, cancellationToken);

        if (!await _db.SosPriorityRuleConfigs.AnyAsync(cancellationToken))
        {
            _db.SosPriorityRuleConfigs.Add(new SosPriorityRuleConfig
            {
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                IsActive = true,
                CreatedAt = seed.AnchorUtc,
                ActivatedAt = seed.AnchorUtc,
                ConfigJson = Json(new { levels = new[] { "Low", "Medium", "High", "Critical" } }),
                IssueWeightsJson = Json(new { unconscious = 5, drowning = 5, breathingDifficulty = 4, fever = 2, trauma = 4 }),
                MedicalSevereIssuesJson = Json(new[] { "unconscious", "drowning", "breathingDifficulty", "trauma", "headInjury", "cannotMove" }),
                AgeWeightsJson = Json(new { child = 1.4, elderly = 1.3, adult = 1.0, pregnant = 1.35 }),
                RequestTypeScoresJson = Json(new { Rescue = 30, Relief = 18, Both = 40 }),
                SituationMultipliersJson = Json(new[]
                {
                    new { keys = new[] { "Flooding" }, multiplier = 1.2, severe = true },
                    new { keys = new[] { "Collapsed", "Landslide" }, multiplier = 1.2, severe = true },
                    new { keys = new[] { "Trapped", "DangerZone", "Stranded" }, multiplier = 1.15, severe = true },
                    new { keys = new[] { "CannotMove", "Medical" }, multiplier = 1.1, severe = true },
                    new { keys = new[] { "Other", "DEFAULT_WHEN_NULL" }, multiplier = 1.05, severe = false }
                }),
                PriorityThresholdsJson = Json(new
                {
                    critical = new { minScore = 80 },
                    high = new { minScore = 60 },
                    medium = new { minScore = 35 },
                    low = new { minScore = 0 }
                }),
                WaterUrgencyScoresJson = Json(new { none = 0, low = 2, medium = 5, high = 8 }),
                FoodUrgencyScoresJson = Json(new { none = 0, oneDay = 3, twoDays = 6, critical = 9 }),
                BlanketUrgencyRulesJson = Json(new { elderly = 4, child = 4, coldRain = 3 }),
                ClothingUrgencyRulesJson = Json(new { soaked = 5, child = 3 }),
                VulnerabilityRulesJson = Json(new { children = 3, elderly = 3, pregnant = 4, injured = 5 }),
                VulnerabilityScoreExpressionJson = "{}",
                ReliefScoreExpressionJson = "{}",
                PriorityScoreExpressionJson = "{}",
                UpdatedAt = seed.AnchorUtc
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
