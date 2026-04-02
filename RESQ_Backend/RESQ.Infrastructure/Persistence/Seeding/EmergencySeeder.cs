using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Domain.Enum.Emergency;
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
        var now = new DateTime(2025, 10, 15, 8, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<SosCluster>().HasData(
            // Cluster 1: Thừa Thiên Huế — Lũ lụt nghiêm trọng
            // Tâm cụm gần cặp SOS A (Id=1, Id=2) — PENDING: chờ coordinator gom SOS và tạo mission
            new SosCluster
            {
                Id = 1,
                CenterLocation = new Point(107.568, 16.455) { SRID = 4326 }, // Gần Depot 1 (Huế)
                RadiusKm = 5.0,
                SeverityLevel = "Critical",
                WaterLevel = "Ngập sâu 2.5m",
                VictimEstimated = 150,
                ChildrenCount = 40,
                ElderlyCount = 50,
                MedicalUrgencyScore = 0.9,
                CreatedAt = now,
                LastUpdatedAt = now,
                IsMissionCreated = false
            },
            // Cluster 2: Đà Nẵng — Sử dụng cho AI analysis, không có SOS request gắn trực tiếp
            new SosCluster
            {
                Id = 2,
                CenterLocation = new Point(108.222, 16.080) { SRID = 4326 }, // Gần Depot 2 (Đà Nẵng)
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
            // Cluster 3: Hà Tĩnh — Sạt lở + cô lập
            // Tâm cụm gần cặp SOS B (Id=3, Id=4) — PENDING: chờ coordinator gom SOS và tạo mission
            new SosCluster
            {
                Id = 3,
                CenterLocation = new Point(105.901, 18.350) { SRID = 4326 }, // Gần Depot 3 (Hà Tĩnh)
                RadiusKm = 3.5,
                SeverityLevel = "High",
                WaterLevel = "Ngập 0.8m, đường bị chia cắt",
                VictimEstimated = 85,
                ChildrenCount = 20,
                ElderlyCount = 25,
                MedicalUrgencyScore = 0.7,
                CreatedAt = now,
                LastUpdatedAt = now
            },
            // Cluster 4: Phong Điền, Thừa Thiên Huế — Đã có Mission #3 Completed
            // Tâm cụm gần SOS G (Id=7, Id=8)
            new SosCluster
            {
                Id = 4,
                CenterLocation = new Point(107.582, 16.465) { SRID = 4326 }, // Phong Điền, Huế
                RadiusKm = 4.0,
                SeverityLevel = "High",
                WaterLevel = "Ngập 1.5m, thôn bị cô lập",
                VictimEstimated = 80,
                ChildrenCount = 20,
                ElderlyCount = 15,
                MedicalUrgencyScore = 0.65,
                CreatedAt = new DateTime(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 3, 1, 15, 0, 0, DateTimeKind.Utc),
                IsMissionCreated = true
            }
        );
    }

    private static void SeedSosRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SosRequest>().HasData(

            // ============================================================
            // CẶP A — 2 yêu cầu gần nhau (~700m), TP. Huế
            // ============================================================

            // A-1: Nhà bị ngập tầng 2, có cụ bà 82t bị liệt
            // Người gửi: Hoàng Văn (victim) — 0945678901
            new SosRequest
            {
                Id = 1,
                PacketId = Guid.Parse("A1000000-0000-0000-0000-000000000001"),
                ClusterId = null,
                UserId = SeedConstants.VictimUserId,
                Location = new Point(107.567, 16.454) { SRID = 4326 }, // Gần Depot 1 (Huế)
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
                Timestamp = 1760517000L, // 2025-10-15 08:30 UTC
                PriorityLevel = SosPriorityLevel.Critical.ToString(),
                Status = SosRequestStatus.Pending.ToString(),
                CreatedAt = new DateTime(2025, 10, 15, 8, 30, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2025, 10, 15, 8, 30, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2025, 10, 15, 8, 30, 0, DateTimeKind.Utc),
                ReviewedAt = null,
                ReviewedById = null,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"critical","suggested_severity":"Critical","confidence":0.96,"risk_factors":["deep_flooding","elderly_paralyzed","water_rising"],"needs":["boat_rescue","medical_team"]}"""
            },

            // A-2: Phụ nữ mang thai tháng 8 + 3 trẻ nhỏ, đang trú trên mái — ~700m từ A-1
            // Người gửi: Nguyễn Thanh Tùng (applicant1) — 0961111111
            new SosRequest
            {
                Id = 2,
                PacketId = Guid.Parse("A2000000-0000-0000-0000-000000000002"),
                ClusterId = null,
                UserId = SeedConstants.Applicant1UserId,
                Location = new Point(107.569, 16.456) { SRID = 4326 }, // Gần Depot 1 (Huế)
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
                Timestamp = 1763627700L, // 2025-11-20 08:35 UTC
                PriorityLevel = SosPriorityLevel.Critical.ToString(),
                Status = SosRequestStatus.Pending.ToString(),
                CreatedAt = new DateTime(2025, 11, 20, 8, 35, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2025, 11, 20, 8, 35, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2025, 11, 20, 8, 35, 0, DateTimeKind.Utc),
                ReviewedAt = null,
                ReviewedById = null,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"critical","suggested_severity":"Critical","confidence":0.94,"risk_factors":["pregnant_woman","young_children","rooftop_refuge"],"needs":["boat_rescue","obstetrics_support"]}"""
            },

            // ============================================================
            // CẶP B — 2 yêu cầu gần nhau (~800m), TP. Hà Tĩnh
            // Cách cặp A khoảng 160km về phía bắc
            // ============================================================

            // B-1: Sạt lở đường, xe tải bị chặn, 2 người bị thương (gãy tay + chảy máu đầu)
            // Người gửi: Trần Minh Đức (applicant2) — 0962222222
            new SosRequest
            {
                Id = 3,
                PacketId = Guid.Parse("B3000000-0000-0000-0000-000000000003"),
                ClusterId = null,
                UserId = SeedConstants.Applicant2UserId,
                Location = new Point(105.902, 18.351) { SRID = 4326 }, // Gần Depot 3 (Hà Tĩnh)
                LocationAccuracy = 6,
                SosType = "RESCUE",
                OriginId = "D1B00003-0000-4A8A-B0BF-000000000003",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 5 | Bị thương: Người lớn 1: Gãy tay (Trung bình); Người lớn 2: Chảy máu đầu (Nghiêm trọng) | Ghi chú: Sat lo dat chan duong tinh lo, xe tai bi chan lai, can truc thang hoac di bo rung",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "LANDSLIDE",
                        "can_move": false,
                        "has_injured": true,
                        "need_medical": true,
                        "others_are_stable": false,
                        "people_count": { "adult": 5, "child": 0, "elderly": 0 },
                        "additional_description": "Sat lo dat chan duong tinh lo, 2 nguoi bi thuong nang, can truc thang hoac di bo rung"
                      },
                      "group_needs": {
                        "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                        "medicine": {
                          "needs_urgent_medicine": true,
                          "conditions": ["FRACTURE", "BLEEDING"]
                        }
                      },
                      "victims": [
                        {
                          "index": 1,
                          "person_type": "adult",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "Moderate",
                            "medical_issues": ["FRACTURE"]
                          }
                        },
                        {
                          "index": 2,
                          "person_type": "adult",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "Critical",
                            "medical_issues": ["BLEEDING"]
                          }
                        }
                      ],
                      "prepared_profiles": []
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
                Timestamp = 1766047500L, // 2025-12-18 08:45 UTC
                PriorityLevel = SosPriorityLevel.High.ToString(),
                Status = SosRequestStatus.Pending.ToString(),
                CreatedAt = new DateTime(2025, 12, 18, 8, 45, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2025, 12, 18, 8, 45, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2025, 12, 18, 8, 45, 0, DateTimeKind.Utc),
                ReviewedAt = null,
                ReviewedById = null,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.89,"risk_factors":["landslide","bleeding_injury","road_blocked"],"needs":["medical_evacuation","helicopter"]}"""
            },

            // B-2: Cầu bị ngập, cả thôn cô lập 3 ngày, thiếu sữa + thuốc — ~800m từ B-1
            // Người gửi: Lê Thị Hương (applicant3) — 0963333333
            new SosRequest
            {
                Id = 4,
                PacketId = Guid.Parse("B4000000-0000-0000-0000-000000000004"),
                ClusterId = null,
                UserId = SeedConstants.Applicant3UserId,
                Location = new Point(105.899, 18.349) { SRID = 4326 }, // Gần Depot 3 (Hà Tĩnh)
                LocationAccuracy = 15,
                SosType = "RESCUE",
                OriginId = "D1B00004-0000-4A8A-B0BF-000000000004",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị cô lập | Số người: 120 | Trẻ em: 4 | Người già: 25 | Bị thương: Người lớn 1: Bệnh nền (Trung bình) | Ghi chú: Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be can sua gap, nguoi gia het thuoc huyet ap",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "FLOODING",
                        "can_move": false,
                        "has_injured": true,
                        "need_medical": true,
                        "others_are_stable": false,
                        "people_count": { "adult": 91, "child": 4, "elderly": 25 },
                        "additional_description": "Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be duoi 1 tuoi can sua gap, nguoi gia het thuoc huyet ap"
                      },
                      "group_needs": {
                        "supplies": ["FOOD", "WATER", "MEDICINE"],
                        "medicine": {
                          "needs_urgent_medicine": true,
                          "conditions": ["CHRONIC_DISEASE"]
                        }
                      },
                      "victims": [
                        {
                          "index": 1,
                          "person_type": "elderly",
                          "custom_name": "Người già bệnh huyết áp",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "Moderate",
                            "medical_issues": ["CHRONIC_DISEASE"]
                          }
                        }
                      ],
                      "prepared_profiles": []
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
                Timestamp = 1769071920L, // 2026-01-22 08:50 UTC
                PriorityLevel = SosPriorityLevel.High.ToString(),
                Status = SosRequestStatus.Pending.ToString(),
                CreatedAt = new DateTime(2026, 1, 22, 8, 50, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 1, 22, 8, 52, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 1, 22, 8, 52, 0, DateTimeKind.Utc),
                ReviewedAt = null,
                ReviewedById = null,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.91,"risk_factors":["prolonged_isolation","infant_malnutrition","medication_shortage"],"needs":["food_drop","water","medicine"]}"""
            },

            // YÊU CẦU ĐƠN LẺ — TP. Đà Nẵng
            // Không thuộc cluster nào

            // Người đi rừng bị lạc, gãy chân trái, điện thoại sắp hết pin
            // Người gửi: Phạm Văn Hải (applicant4) — 0964444444
            new SosRequest
            {
                Id = 5,
                PacketId = Guid.Parse("C5000000-0000-0000-0000-000000000005"),
                ClusterId = 2,
                UserId = SeedConstants.Applicant4UserId,
                Location = new Point(108.221, 16.081) { SRID = 4326 }, // Gần Depot 2 (Đà Nẵng)
                LocationAccuracy = 5,
                SosType = "RESCUE",
                OriginId = "D1C00005-0000-4A8A-B0BF-000000000005",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 1 | Bị thương: Người lớn 1: Gãy chân (Nghiêm trọng) | Ghi chú: Lac trong rung Hoa Phu, gay chan trai khong di duoc, dien thoai sap het pin 8%",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "ACCIDENT",
                        "can_move": false,
                        "has_injured": true,
                        "need_medical": true,
                        "others_are_stable": true,
                        "people_count": { "adult": 1, "child": 0, "elderly": 0 },
                        "additional_description": "Lac trong rung Hoa Phu tu sang, gay chan trai khong tu di duoc, dien thoai con 8% pin, toa do GPS 16.0240N 108.0100E"
                      },
                      "group_needs": {
                        "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                        "medicine": {
                          "needs_urgent_medicine": true,
                          "conditions": ["FRACTURE"]
                        }
                      },
                      "victims": [
                        {
                          "index": 1,
                          "person_type": "adult",
                          "custom_name": "Bản thân",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "Critical",
                            "medical_issues": ["FRACTURE"]
                          }
                        }
                      ],
                      "prepared_profiles": []
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
                Timestamp = 1772010600L, // 2026-02-25 09:10 UTC
                PriorityLevel = SosPriorityLevel.High.ToString(),
                Status = SosRequestStatus.Assigned.ToString(),
                CreatedAt = new DateTime(2026, 2, 25, 9, 10, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 2, 25, 9, 10, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 2, 25, 9, 10, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2026, 2, 25, 9, 20, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"medium","suggested_severity":"Moderate","confidence":0.85,"risk_factors":["isolated_forest","broken_leg","low_battery"],"needs":["search_rescue","medical"]}"""
            },

            // ============================================================
            // SOS #6 — Huế (Cluster chưa có): Scenario 1 "SOS mới đến"
            // Người gửi: victim (55555555) — mới nhất, xuất hiện đầu dashboard
            // ============================================================
            new SosRequest
            {
                Id = 6,
                PacketId = Guid.Parse("D6000000-0000-0000-0000-000000000006"),
                ClusterId = null,
                UserId = SeedConstants.VictimUserId,
                Location = new Point(107.583, 16.466) { SRID = 4326 }, // Phong Điền, Huế
                LocationAccuracy = 10,
                SosType = "RESCUE",
                OriginId = "D1D00006-0000-4A8A-B0BF-000000000006",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị mắc kẹt | Số người: 4 | Người già: 2 | Ghi chú: Nuoc lu dang len cao, can xuong can to de chuyen nguoi gia ra, 2 cu gia khong di chuyen duoc",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "FLOODING",
                        "can_move": false,
                        "has_injured": false,
                        "need_medical": false,
                        "others_are_stable": false,
                        "people_count": { "adult": 2, "child": 0, "elderly": 2 },
                        "additional_description": "Nuoc lu dang len cao, can xuong can to de chuyen nguoi gia ra, 2 cu gia khong di chuyen duoc"
                      },
                      "group_needs": {
                        "supplies": ["WATER", "TRANSPORTATION"]
                      },
                      "victims": [],
                      "prepared_profiles": []
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 0,
                      "path": ["D1D00006-0000-4A8A-B0BF-000000000006"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1D00006-0000-4A8A-B0BF-000000000006",
                      "is_online": true,
                      "user_id": "55555555-5555-5555-5555-555555555555",
                      "user_name": "0945678901",
                      "user_phone": "0945678901"
                    }
                    """,
                Timestamp = 1775030400L, // 2026-04-01 06:40 UTC
                PriorityLevel = SosPriorityLevel.High.ToString(),
                Status = SosRequestStatus.Pending.ToString(),
                CreatedAt = new DateTime(2026, 4, 1, 6, 40, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 4, 1, 6, 40, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 4, 1, 6, 40, 0, DateTimeKind.Utc),
                ReviewedAt = null,
                ReviewedById = null,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.88,"risk_factors":["flooding","elderly_immobile","water_rising"],"needs":["boat","evacuation"]}"""
            },

            // ============================================================
            // SOS #7 — Phong Điền, Huế (Cluster 4): Scenario 4 "Đã hoàn thành"
            // Người gửi: applicant3 (66666666-...-6663) — 0963333333
            // ============================================================
            new SosRequest
            {
                Id = 7,
                PacketId = Guid.Parse("E7000000-0000-0000-0000-000000000007"),
                ClusterId = 4,
                UserId = SeedConstants.Applicant3UserId,
                Location = new Point(107.580, 16.463) { SRID = 4326 }, // Phong Điền, Huế
                LocationAccuracy = 8,
                SosType = "RESCUE",
                OriginId = "D1G00007-0000-4A8A-B0BF-000000000007",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị mắc kẹt | Số người: 7 | Trẻ em: 3 | Người già: 1 | Ghi chú: Nuoc lu ngang mat nen, can xuong cuu nguoi va hang cu tro can thiet",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "FLOODING",
                        "can_move": false,
                        "has_injured": false,
                        "need_medical": false,
                        "others_are_stable": false,
                        "people_count": { "adult": 3, "child": 3, "elderly": 1 },
                        "additional_description": "Nuoc lu ngang mat nen, can xuong cuu nguoi va hang cu tro can thiet"
                      },
                      "group_needs": {
                        "supplies": ["FOOD", "WATER", "TRANSPORTATION"]
                      },
                      "victims": [],
                      "prepared_profiles": []
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 0,
                      "path": ["D1G00007-0000-4A8A-B0BF-000000000007"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1G00007-0000-4A8A-B0BF-000000000007",
                      "is_online": false,
                      "user_id": "66666666-6666-6666-6666-666666666663",
                      "user_name": "0963333333",
                      "user_phone": "0963333333"
                    }
                    """,
                Timestamp = 1772038200L, // 2026-03-01 07:10 UTC
                PriorityLevel = SosPriorityLevel.High.ToString(),
                Status = SosRequestStatus.Resolved.ToString(),
                CreatedAt = new DateTime(2026, 3, 1, 7, 10, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 3, 1, 7, 10, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 3, 1, 14, 0, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2026, 3, 1, 7, 30, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"high","suggested_severity":"High","confidence":0.87,"risk_factors":["flooding","family_with_children","elderly"],"needs":["boat","evacuation","food_water"]}"""
            },

            // ============================================================
            // SOS #8 — Phong Điền, Huế (Cluster 4): Scenario 4 "Đã hoàn thành"
            // Người gửi: applicant4 (66666666-...-6664) — 0964444444
            // ============================================================
            new SosRequest
            {
                Id = 8,
                PacketId = Guid.Parse("E8000000-0000-0000-0000-000000000008"),
                ClusterId = 4,
                UserId = SeedConstants.Applicant4UserId,
                Location = new Point(107.584, 16.467) { SRID = 4326 }, // Phong Điền, Huế (~500m từ SOS 7)
                LocationAccuracy = 12,
                SosType = "RESCUE",
                OriginId = "D1G00008-0000-4A8A-B0BF-000000000008",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị mắc kẹt | Số người: 3 | Bị thương: Người lớn 1: Trầy xước (Nhẹ) | Ghi chú: Nuoc vao nha cap 1, nguoi bi thuong nhe do leo len mai, can ho tro di tan",
                StructuredData = """
                    {
                      "incident": {
                        "situation": "FLOODING",
                        "can_move": false,
                        "has_injured": true,
                        "need_medical": false,
                        "others_are_stable": true,
                        "people_count": { "adult": 2, "child": 0, "elderly": 1 },
                        "additional_description": "Nuoc vao nha cap 1, nguoi bi thuong nhe do leo len mai, can ho tro di tan"
                      },
                      "group_needs": {
                        "supplies": ["TRANSPORTATION"]
                      },
                      "victims": [
                        {
                          "index": 1,
                          "person_type": "adult",
                          "incident_status": {
                            "is_injured": true,
                            "severity": "Minor",
                            "medical_issues": ["MINOR_INJURY"]
                          }
                        }
                      ],
                      "prepared_profiles": []
                    }
                    """,
                NetworkMetadata = """
                    {
                      "hop_count": 1,
                      "path": ["D1G00007-0000-4A8A-B0BF-000000000007", "D1G00008-0000-4A8A-B0BF-000000000008"]
                    }
                    """,
                SenderInfo = """
                    {
                      "device_id": "D1G00008-0000-4A8A-B0BF-000000000008",
                      "is_online": false,
                      "user_id": "66666666-6666-6666-6666-666666666664",
                      "user_name": "0964444444",
                      "user_phone": "0964444444"
                    }
                    """,
                Timestamp = 1772039100L, // 2026-03-01 07:25 UTC
                PriorityLevel = SosPriorityLevel.Medium.ToString(),
                Status = SosRequestStatus.Resolved.ToString(),
                CreatedAt = new DateTime(2026, 3, 1, 7, 25, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 3, 1, 7, 25, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2026, 3, 1, 14, 0, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2026, 3, 1, 7, 35, 0, DateTimeKind.Utc),
                ReviewedById = SeedConstants.CoordinatorUserId,
                CreatedByCoordinatorId = null,
                AiAnalysis = """{"urgency":"medium","suggested_severity":"Moderate","confidence":0.83,"risk_factors":["flooding","minor_injury","elderly"],"needs":["evacuation"]}"""
            }
        );
    }

    private static void SeedSosRuleEvaluations(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 10, 15, 8, 31, 0, DateTimeKind.Utc);

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
                PriorityLevel = SosPriorityLevel.Critical.ToString(),
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
                PriorityLevel = SosPriorityLevel.Critical.ToString(),
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
                PriorityLevel = SosPriorityLevel.High.ToString(),
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
                PriorityLevel = SosPriorityLevel.High.ToString(),
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
                PriorityLevel = SosPriorityLevel.High.ToString(),
                RuleVersion = "1.0",
                ItemsNeeded = "[\"FIRST_AID_KIT\",\"MEDICAL_SUPPLIES\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = now.AddMinutes(2)
            },
            // SOS 6: Lũ lụt Phong Điền, 2 người già không di chuyển → High (58.0)
            new SosRuleEvaluation
            {
                Id = 6,
                SosRequestId = 6,
                MedicalScore = 0.0,    // need_medical=false
                InjuryScore = 30.0,    // has_injured=false(0) + others_not_stable(30)
                MobilityScore = 80.0,  // can_move=false
                EnvironmentScore = 80.0, // RESCUE(40) + FLOODING(40)
                FoodScore = 60.0,      // 4 people(40) + elderly(15) = min(40,50)=40+15=55→clamp
                TotalScore = 58.0,
                PriorityLevel = SosPriorityLevel.High.ToString(),
                RuleVersion = "1.0",
                ItemsNeeded = "[\"LIFE_JACKET\",\"RESCUE_BOAT\",\"ROPE\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = new DateTime(2026, 4, 1, 6, 42, 0, DateTimeKind.Utc)
            },
            // SOS 7: Phong Điền, gia đình 7 người (3 trẻ, 1 người già) → High Resolved
            new SosRuleEvaluation
            {
                Id = 7,
                SosRequestId = 7,
                MedicalScore = 0.0,    // need_medical=false
                InjuryScore = 30.0,    // has_injured=false(0) + others_not_stable(30)
                MobilityScore = 80.0,
                EnvironmentScore = 80.0, // RESCUE(40) + FLOODING(40)
                FoodScore = 80.0,      // min(7*10,50)=50 + child(15) + elderly(15)
                TotalScore = 60.0,
                PriorityLevel = SosPriorityLevel.High.ToString(),
                RuleVersion = "1.0",
                ItemsNeeded = "[\"LIFE_JACKET\",\"RESCUE_BOAT\",\"ROPE\",\"FOOD_RATIONS\",\"WATER\",\"BLANKETS\"]",
                CreatedAt = new DateTime(2026, 3, 1, 7, 12, 0, DateTimeKind.Utc)
            },
            // SOS 8: Phong Điền, 3 người, 1 thương nhẹ → Medium Resolved
            new SosRuleEvaluation
            {
                Id = 8,
                SosRequestId = 8,
                MedicalScore = 0.0,    // need_medical=false
                InjuryScore = 40.0,    // has_injured(40) + others_stable(0)
                MobilityScore = 80.0,
                EnvironmentScore = 80.0, // RESCUE(40) + FLOODING(40)
                FoodScore = 45.0,      // min(3*10,50)=30 + elderly(15)
                TotalScore = 45.0,
                PriorityLevel = SosPriorityLevel.Medium.ToString(),
                RuleVersion = "1.0",
                ItemsNeeded = "[\"LIFE_JACKET\",\"RESCUE_BOAT\",\"FOOD_RATIONS\",\"WATER\"]",
                CreatedAt = new DateTime(2026, 3, 1, 7, 27, 0, DateTimeKind.Utc)
            }
        );
    }

    private static void SeedSosAiAnalyses(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 10, 15, 8, 31, 0, DateTimeKind.Utc);

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
