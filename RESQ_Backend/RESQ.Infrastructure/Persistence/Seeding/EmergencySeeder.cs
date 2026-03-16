using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class EmergencySeeder
{
    public static void SeedEmergency(this ModelBuilder modelBuilder)
    {
        SeedSosClusters(modelBuilder);
        SeedSosRequests(modelBuilder);
        SeedSosRuleEvaluations(modelBuilder);
        SeedSosAiAnalyses(modelBuilder);
    }

    private static void SeedSosClusters(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosCluster>().HasData(
            // Cluster 1: Lệ Thủy, Quảng Bình — Lũ lụt nghiêm trọng
            // Tâm cụm gần cặp SOS A (Id=1, Id=2)
            new SosCluster
            {
                Id = 1,
                CenterLocation = new Point(106.7885, 17.2168) { SRID = 4326 },
                RadiusKm = 5.0,
                SeverityLevel = "Critical",
                WaterLevel = "Ngập sâu 2.5m",
                VictimEstimated = 150,
                ChildrenCount = 40,
                ElderlyCount = 50,
                MedicalUrgencyScore = 0.9,
                CreatedAt = now,
                LastUpdatedAt = now
            },
            // Cluster 2: Quảng Trị — Sử dụng cho AI analysis, không có SOS request gắn trực tiếp
            new SosCluster
            {
                Id = 2,
                CenterLocation = new Point(107.1021, 17.0190) { SRID = 4326 },
                RadiusKm = 3.0,
                SeverityLevel = "High",
                WaterLevel = "Ngập 1.0m, chảy xiết",
                VictimEstimated = 60,
                ChildrenCount = 15,
                ElderlyCount = 20,
                MedicalUrgencyScore = 0.6,
                CreatedAt = now,
                LastUpdatedAt = now,
                IsMissionCreated = true
            },
            // Cluster 3: Quảng Bình — Sạt lở + cô lập
            // Tâm cụm gần cặp SOS B (Id=3, Id=4)
            new SosCluster
            {
                Id = 3,
                CenterLocation = new Point(106.6067, 17.4812) { SRID = 4326 },
                RadiusKm = 3.5,
                SeverityLevel = "High",
                WaterLevel = "Ngập 0.8m, đường bị chia cắt",
                VictimEstimated = 85,
                ChildrenCount = 20,
                ElderlyCount = 25,
                MedicalUrgencyScore = 0.7,
                CreatedAt = now,
                LastUpdatedAt = now
            }
        );
    }

    private static void SeedSosRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SosRequest>().HasData(

            // ============================================================
            // CẶP A — 2 yêu cầu gần nhau (~700m), Lệ Thủy, Quảng Bình
            // ============================================================

            // A-1: Nhà bị ngập tầng 2, có cụ bà 82t bị liệt
            // Người gửi: Hoàng Văn (victim) — 0945678901
            new SosRequest
            {
                Id = 1,
                PacketId = Guid.Parse("A1000000-0000-0000-0000-000000000001"),
                ClusterId = 1,
                UserId = SeedConstants.VictimUserId,
                Location = new Point(106.7850, 17.2140) { SRID = 4326 }, // Thôn Tân Thủy, Lệ Thủy
                LocationAccuracy = 8,
                SosType = "RESCUE",
                OriginId = "D1A00001-0000-4A8A-B0BF-000000000001",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị mắc kẹt | Số người: 3 | Người già: 1 | Bị thương: Người lớn 1: Bệnh nền (Nghiêm trọng) | Ghi chú: Cu ba 82 tuoi bi liet nua nguoi khong di chuyen duoc, nuoc lu dang len nhanh",
                StructuredData = """
                    {
                      "situation": "FLOODING",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 2, "child": 0, "elderly": 1 },
                      "medical_issues": ["CHRONIC_DISEASE", "MOBILITY_IMPAIRMENT", "BREATHING_DIFFICULTY"],
                      "supplies": ["MEDICINE", "TRANSPORTATION"],
                      "additional_description": "Cu ba 82 tuoi bi liet nua nguoi, kho tho, nuoc lu dang len nhanh khong tu di chuyen duoc",
                      "injured_persons": [
                        {
                          "index": 1,
                          "name": "Người lớn 1",
                          "custom_name": "Cụ bà 82 tuổi",
                          "person_type": "elderly",
                          "medical_issues": ["CHRONIC_DISEASE", "MOBILITY_IMPAIRMENT", "BREATHING_DIFFICULTY"],
                          "severity": "Critical"
                        }
                      ]
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 0,
                      "path": ["D1A00001-0000-4A8A-B0BF-000000000001"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1A00001-0000-4A8A-B0BF-000000000001",
                      "is_online": true,
                      "user_id": "55555555-5555-5555-5555-555555555555",
                      "user_name": "0945678901",
                      "user_phone": "0945678901"
                    }
                    """,
                Timestamp = 1729067400L, // 2024-10-16 08:30 UTC
                PriorityLevel = "Critical",
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2024, 10, 16, 8, 40, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"critical","suggested_severity":"Critical","confidence":0.96,"risk_factors":["deep_flooding","elderly_paralyzed","water_rising"],"needs":["boat_rescue","medical_team"]}"""
            },

            // A-2: Phụ nữ mang thai tháng 8 + 3 trẻ nhỏ, đang trú trên mái — ~700m từ A-1
            // Người gửi: Nguyễn Thanh Tùng (applicant1) — 0961111111
            new SosRequest
            {
                Id = 2,
                PacketId = Guid.Parse("A2000000-0000-0000-0000-000000000002"),
                ClusterId = 1,
                UserId = SeedConstants.Applicant1UserId,
                Location = new Point(106.7920, 17.2195) { SRID = 4326 }, // Thôn Nộn Kết, Lệ Thủy
                LocationAccuracy = 12,
                SosType = "RESCUE",
                OriginId = "D1A00002-0000-4A8A-B0BF-000000000002",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị mắc kẹt | Số người: 6 | Trẻ em: 3 | Bị thương: Người lớn 1: Thai kỳ tháng 8 (Nghiêm trọng) | Ghi chú: Dang tru tren mai nha 3 tre nho kho tho vi met moi, nuoc van dang dang",
                StructuredData = """
                    {
                      "situation": "FLOODING",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 3, "child": 3, "elderly": 0 },
                      "medical_issues": ["PREGNANCY", "BREATHING_DIFFICULTY"],
                      "supplies": ["MEDICINE", "WATER"],
                      "additional_description": "Dang tru tren mai nha, 3 tre nho kho tho vi met moi, vo mang thai thang 8, nuoc van dang dang",
                      "injured_persons": [
                        {
                          "index": 1,
                          "name": "Người lớn 1",
                          "custom_name": "Vợ mang thai tháng 8",
                          "person_type": "adult",
                          "medical_issues": ["PREGNANCY", "BREATHING_DIFFICULTY"],
                          "severity": "Critical"
                        }
                      ]
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 1,
                      "path": ["D1A00001-0000-4A8A-B0BF-000000000001", "D1A00002-0000-4A8A-B0BF-000000000002"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1A00002-0000-4A8A-B0BF-000000000002",
                      "is_online": true,
                      "user_id": "66666666-6666-6666-6666-666666666661",
                      "user_name": "0961111111",
                      "user_phone": "0961111111"
                    }
                    """,
                Timestamp = 1729067700L, // 2024-10-16 08:35 UTC
                PriorityLevel = "Critical",
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 35, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2024, 10, 16, 8, 35, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 40, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"critical","suggested_severity":"Critical","confidence":0.94,"risk_factors":["pregnant_woman","young_children","rooftop_refuge"],"needs":["boat_rescue","obstetrics_support"]}"""
            },

            // ============================================================
            // CẶP B — 2 yêu cầu gần nhau (~800m), Phong Điền, TT-Huế
            // Cách cặp A khoảng 80km về phía đông nam
            // ============================================================

            // B-1: Sạt lở đường, xe tải bị chặn, 2 người bị thương (gãy tay + chảy máu đầu)
            // Người gửi: Trần Minh Đức (applicant2) — 0962222222
            new SosRequest
            {
                Id = 3,
                PacketId = Guid.Parse("B3000000-0000-0000-0000-000000000003"),
                ClusterId = 3,
                UserId = SeedConstants.Applicant2UserId,
                Location = new Point(106.6100, 17.4780) { SRID = 4326 }, // Thôn Bắc Trạch, Bố Trạch, Quảng Bình
                LocationAccuracy = 6,
                SosType = "RESCUE",
                OriginId = "D1B00003-0000-4A8A-B0BF-000000000003",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 5 | Bị thương: Người lớn 1: Gãy tay (Trung bình); Người lớn 2: Chảy máu đầu (Nghiêm trọng) | Ghi chú: Sat lo dat chan duong tinh lo, xe tai bi chan lai, can truc thang hoac di bo rung",
                StructuredData = """
                    {
                      "situation": "LANDSLIDE",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 5, "child": 0, "elderly": 0 },
                      "medical_issues": ["FRACTURE", "BLEEDING"],
                      "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                      "additional_description": "Sat lo dat chan duong tinh lo, 2 nguoi bi thuong nang, can truc thang hoac di bo rung",
                      "injured_persons": [
                        {
                          "index": 1,
                          "name": "Người lớn 1",
                          "custom_name": null,
                          "person_type": "adult",
                          "medical_issues": ["FRACTURE"],
                          "severity": "Moderate"
                        },
                        {
                          "index": 2,
                          "name": "Người lớn 2",
                          "custom_name": null,
                          "person_type": "adult",
                          "medical_issues": ["BLEEDING"],
                          "severity": "Critical"
                        }
                      ]
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 0,
                      "path": ["D1B00003-0000-4A8A-B0BF-000000000003"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1B00003-0000-4A8A-B0BF-000000000003",
                      "is_online": true,
                      "user_id": "66666666-6666-6666-6666-666666666662",
                      "user_name": "0962222222",
                      "user_phone": "0962222222"
                    }
                    """,
                Timestamp = 1729068300L, // 2024-10-16 08:45 UTC
                PriorityLevel = "High",
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2024, 10, 16, 8, 55, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.89,"risk_factors":["landslide","bleeding_injury","road_blocked"],"needs":["medical_evacuation","helicopter"]}"""
            },

            // B-2: Cầu bị ngập, cả thôn cô lập 3 ngày, thiếu sữa + thuốc — ~800m từ B-1
            // Người gửi: Lê Thị Hương (applicant3) — 0963333333
            new SosRequest
            {
                Id = 4,
                PacketId = Guid.Parse("B4000000-0000-0000-0000-000000000004"),
                ClusterId = 3,
                UserId = SeedConstants.Applicant3UserId,
                Location = new Point(106.6150, 17.4850) { SRID = 4326 }, // Thôn Đại Trạch, Bố Trạch, Quảng Bình
                LocationAccuracy = 15,
                SosType = "RESCUE",
                OriginId = "D1B00004-0000-4A8A-B0BF-000000000004",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị cô lập | Số người: 120 | Trẻ em: 4 | Người già: 25 | Bị thương: Người lớn 1: Bệnh nền (Trung bình) | Ghi chú: Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be can sua gap, nguoi gia het thuoc huyet ap",
                StructuredData = """
                    {
                      "situation": "FLOODING",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 91, "child": 4, "elderly": 25 },
                      "medical_issues": ["CHRONIC_DISEASE"],
                      "supplies": ["FOOD", "WATER", "MEDICINE"],
                      "additional_description": "Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be duoi 1 tuoi can sua gap, nguoi gia het thuoc huyet ap",
                      "injured_persons": [
                        {
                          "index": 1,
                          "name": "Người lớn 1",
                          "custom_name": "Người già bệnh huyết áp",
                          "person_type": "elderly",
                          "medical_issues": ["CHRONIC_DISEASE"],
                          "severity": "Moderate"
                        }
                      ]
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 2,
                      "path": ["D1B00003-0000-4A8A-B0BF-000000000003", "D1B00004-0000-4A8A-B0BF-000000000004", "D1B00005-RELAY-4A8A-B0BF-000000000005"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1B00004-0000-4A8A-B0BF-000000000004",
                      "is_online": false,
                      "user_id": "66666666-6666-6666-6666-666666666663",
                      "user_name": "0963333333",
                      "user_phone": "0963333333"
                    }
                    """,
                Timestamp = 1729068600L, // 2024-10-16 08:50 UTC
                PriorityLevel = "High",
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 50, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2024, 10, 16, 8, 52, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2024, 10, 16, 9, 0, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.91,"risk_factors":["prolonged_isolation","infant_malnutrition","medication_shortage"],"needs":["food_drop","water","medicine"]}"""
            },

            // ============================================================
            // YÊU CẦU ĐƠN LẺ — Hòa Vang, Đà Nẵng
            // Cách cặp B ~95km, cách cặp A ~170km — không thuộc cluster nào
            // ============================================================

            // Người đi rừng bị lạc, gãy chân trái, điện thoại sắp hết pin
            // Người gửi: Phạm Văn Hải (applicant4) — 0964444444
            new SosRequest
            {
                Id = 5,
                PacketId = Guid.Parse("C5000000-0000-0000-0000-000000000005"),
                ClusterId = 2,
                UserId = SeedConstants.Applicant4UserId,
                Location = new Point(107.1050, 17.0150) { SRID = 4326 }, // Vĩnh Linh, Quảng Trị
                LocationAccuracy = 5,
                SosType = "RESCUE",
                OriginId = "D1C00005-0000-4A8A-B0BF-000000000005",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 1 | Bị thương: Người lớn 1: Gãy chân (Nghiêm trọng) | Ghi chú: Lac trong rung Hoa Phu, gay chan trai khong di duoc, dien thoai sap het pin 8%",
                StructuredData = """
                    {
                      "situation": "ACCIDENT",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": true,
                      "people_count": { "adult": 1, "child": 0, "elderly": 0 },
                      "medical_issues": ["FRACTURE"],
                      "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                      "additional_description": "Lac trong rung Hoa Phu tu sang, gay chan trai khong tu di duoc, dien thoai con 8% pin, toa do GPS 16.0240N 108.0100E",
                      "injured_persons": [
                        {
                          "index": 1,
                          "name": "Người lớn 1",
                          "custom_name": "Bản thân",
                          "person_type": "adult",
                          "medical_issues": ["FRACTURE"],
                          "severity": "Critical"
                        }
                      ]
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 0,
                      "path": ["D1C00005-0000-4A8A-B0BF-000000000005"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1C00005-0000-4A8A-B0BF-000000000005",
                      "is_online": true,
                      "user_id": "66666666-6666-6666-6666-666666666664",
                      "user_name": "0964444444",
                      "user_phone": "0964444444"
                    }
                    """,
                Timestamp = 1729069800L, // 2024-10-16 09:10 UTC
                PriorityLevel = "High",
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2024, 10, 16, 9, 20, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"medium","suggested_severity":"Moderate","confidence":0.85,"risk_factors":["isolated_forest","broken_leg","low_battery"],"needs":["search_rescue","medical"]}"""
            }
        );
    }

    private static void SeedSosRuleEvaluations(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 31, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosRuleEvaluation>().HasData(
            // SOS 1: Lũ lụt, cụ bà 82t liệt + khó thở → Critical (72.5)
            new SosRuleEvaluation
            {
                Id = 1,
                SosRequestId = 1,
                MedicalScore = 75.0,   // need_medical(30) + 3 issues(30) + BREATHING_DIFFICULTY(15)
                InjuryScore = 70.0,    // has_injured(40) + others_not_stable(30)
                MobilityScore = 80.0,  // can_move=false
                EnvironmentScore = 80.0, // RESCUE(40) + FLOODING(40)
                FoodScore = 45.0,      // 3 people(30) + elderly(15)
                TotalScore = 72.5,
                PriorityLevel = "Critical",
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"LIFE_JACKET\",\"RESCUE_BOAT\",\"ROPE\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now
            },
            // SOS 2: Lũ lụt, phụ nữ thai 8 tháng + 3 trẻ em trên mái nhà → Critical (71.5)
            new SosRuleEvaluation
            {
                Id = 2,
                SosRequestId = 2,
                MedicalScore = 65.0,   // need_medical(30) + 2 issues(20) + BREATHING_DIFFICULTY(15)
                InjuryScore = 70.0,    // has_injured(40) + others_not_stable(30)
                MobilityScore = 80.0,
                EnvironmentScore = 80.0,
                FoodScore = 65.0,      // min(6*10,50)=50 + child(15)
                TotalScore = 71.5,
                PriorityLevel = "Critical",
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"LIFE_JACKET\",\"RESCUE_BOAT\",\"ROPE\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now.AddSeconds(30)
            },
            // SOS 3: Sạt lở, 2 người thương nặng (gãy tay + chảy máu đầu) → High (68.5)
            new SosRuleEvaluation
            {
                Id = 3,
                SosRequestId = 3,
                MedicalScore = 60.0,   // need_medical(30) + 2 issues(20) + BLEEDING(10)
                InjuryScore = 70.0,    // has_injured(40) + others_not_stable(30)
                MobilityScore = 80.0,
                EnvironmentScore = 80.0, // RESCUE(40) + LANDSLIDE(40)
                FoodScore = 50.0,      // min(5*10,50)=50
                TotalScore = 68.5,
                PriorityLevel = "High",
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"BANDAGES\",\"BLOOD_CLOTTING_AGENTS\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now.AddMinutes(1)
            },
            // SOS 4: Lũ cô lập cả thôn 120 người, 3 ngày → High (55.5)
            new SosRuleEvaluation
            {
                Id = 4,
                SosRequestId = 4,
                MedicalScore = 40.0,   // need_medical(30) + 1 issue(10)
                InjuryScore = 30.0,    // has_injured=false(0) + others_not_stable(30)
                MobilityScore = 80.0,
                EnvironmentScore = 80.0, // RESCUE(40) + FLOODING(40)
                FoodScore = 80.0,      // min(120*10,50)=50 + child(15) + elderly(15)
                TotalScore = 55.5,
                PriorityLevel = "High",
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"LIFE_JACKET\",\"RESCUE_BOAT\",\"ROPE\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now.AddMinutes(1).AddSeconds(30)
            },
            // SOS 5: Tai nạn rừng, gãy chân, điện thoại 8% pin → High (50.0)
            new SosRuleEvaluation
            {
                Id = 5,
                SosRequestId = 5,
                MedicalScore = 40.0,   // need_medical(30) + 1 issue(10)
                InjuryScore = 40.0,    // has_injured(40) + others_stable(0)
                MobilityScore = 80.0,
                EnvironmentScore = 75.0, // RESCUE(40) + ACCIDENT(35)
                FoodScore = 10.0,      // 1 person(10)
                TotalScore = 50.0,
                PriorityLevel = "High",
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now.AddMinutes(2)
            }
        );
    }

    private static void SeedSosAiAnalyses(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 10, 16, 8, 31, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosAiAnalysis>().HasData(
            new SosAiAnalysis
            {
                Id = 1,
                SosRequestId = 1,
                Metadata = "{\"urgency\": \"critical\", \"risk_factors\": [\"deep_flooding\", \"elderly_paralyzed\", \"water_rising\"], \"needs\": [\"boat_rescue\", \"medical_team\"]}",
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SOS_ASSESSMENT",
                SuggestedSeverityLevel = "Critical",
                SuggestedPriority = "Critical",
                SuggestionScope = "SOS_REQUEST",
                ConfidenceScore = 0.96,
                Explanation = "Phát hiện: người già liệt không tự di chuyển, nước vẫn dâng, không có thuyền. Nguy cơ chết đuối rất cao. Cần can thiệp ngay lập tức.",
                CreatedAt = now,
                AdoptedAt = now.AddMinutes(5)
            },
            new SosAiAnalysis
            {
                Id = 2,
                SosRequestId = 2,
                Metadata = "{\"urgency\": \"critical\", \"risk_factors\": [\"pregnant_woman\", \"young_children\", \"rooftop_refuge\"], \"needs\": [\"boat_rescue\", \"obstetrics_support\"]}",
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SOS_ASSESSMENT",
                SuggestedSeverityLevel = "Critical",
                SuggestedPriority = "Critical",
                SuggestionScope = "SOS_REQUEST",
                ConfidenceScore = 0.94,
                Explanation = "Phát hiện: phụ nữ mang thai tháng 8, 3 trẻ em từ 2-7 tuổi đang trú trên mái nhà. Rủi ro sinh non và chấn thương trẻ em rất cao.",
                CreatedAt = now.AddSeconds(30),
                AdoptedAt = now.AddMinutes(5).AddSeconds(30)
            },
            new SosAiAnalysis
            {
                Id = 3,
                SosRequestId = 3,
                Metadata = "{\"urgency\": \"high\", \"risk_factors\": [\"landslide\", \"bleeding_injury\", \"road_blocked\"], \"needs\": [\"medical_evacuation\", \"helicopter\"]}",
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SOS_ASSESSMENT",
                SuggestedSeverityLevel = "High",
                SuggestedPriority = "High",
                SuggestionScope = "SOS_REQUEST",
                ConfidenceScore = 0.89,
                Explanation = "Phát hiện: sạt lở đường, người bị thương chảy máu đầu (nguy cơ chấn thương sọ não). Đường bộ bị chặn hoàn toàn, cần trực thăng hoặc đường thủy.",
                CreatedAt = now.AddMinutes(1),
                AdoptedAt = now.AddMinutes(6)
            },
            new SosAiAnalysis
            {
                Id = 4,
                SosRequestId = 4,
                Metadata = "{\"urgency\": \"high\", \"risk_factors\": [\"prolonged_isolation\", \"infant_malnutrition\", \"medication_shortage\"], \"needs\": [\"food_drop\", \"water\", \"medicine\"]}",
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SOS_ASSESSMENT",
                SuggestedSeverityLevel = "High",
                SuggestedPriority = "High",
                SuggestionScope = "SOS_REQUEST",
                ConfidenceScore = 0.91,
                Explanation = "Phát hiện: cô lập 3 ngày, 4 trẻ sơ sinh thiếu sữa, người cao tuổi hết thuốc huyết áp. Nguy cơ suy dinh dưỡng trẻ em và biến chứng tim mạch ở người già.",
                CreatedAt = now.AddMinutes(1).AddSeconds(30),
                AdoptedAt = now.AddMinutes(6).AddSeconds(30)
            },
            new SosAiAnalysis
            {
                Id = 5,
                SosRequestId = 5,
                Metadata = "{\"urgency\": \"medium\", \"risk_factors\": [\"isolated_forest\", \"broken_leg\", \"low_battery\"], \"needs\": [\"search_rescue\", \"medical\"]}",
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SOS_ASSESSMENT",
                SuggestedSeverityLevel = "Moderate",
                SuggestedPriority = "Moderate",
                SuggestionScope = "SOS_REQUEST",
                ConfidenceScore = 0.85,
                Explanation = "Phát hiện: người một mình bị gãy chân trong rừng sâu, điện thoại sắp hết pin. Vị trí GPS xác định. Ưu tiên triển khai đội tìm kiếm cứu nạn rừng núi.",
                CreatedAt = now.AddMinutes(2),
                AdoptedAt = now.AddMinutes(7)
            }
        );
    }
}
