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
            // Cluster 2: Hương Trà, Huế — Sử dụng cho AI analysis, không có SOS request gắn trực tiếp
            new SosCluster
            {
                Id = 2,
                CenterLocation = new Point(107.4566, 16.3986) { SRID = 4326 },
                RadiusKm = 3.0,
                SeverityLevel = "High",
                WaterLevel = "Ngập 1.0m, chảy xiết",
                VictimEstimated = 60,
                ChildrenCount = 15,
                ElderlyCount = 20,
                MedicalUrgencyScore = 0.6,
                CreatedAt = now,
                LastUpdatedAt = now
            },
            // Cluster 3: Phong Điền, Thừa Thiên-Huế — Sạt lở + cô lập
            // Tâm cụm gần cặp SOS B (Id=3, Id=4), cách cặp A ~80km
            new SosCluster
            {
                Id = 3,
                CenterLocation = new Point(107.2908, 16.6365) { SRID = 4326 },
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
                      "situation": "TRAPPED",
                      "can_move": false,
                      "has_injured": false,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 2, "child": 0, "elderly": 1 },
                      "medical_issues": ["CHRONIC_DISEASE", "MOBILITY_IMPAIRMENT"],
                      "supplies": ["MEDICINE", "TRANSPORTATION"],
                      "additional_description": "Cu ba 82 tuoi bi liet nua nguoi khong di chuyen duoc, nuoc lu dang len nhanh"
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
                WaitTimeMinutes = 55,
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 30, 0, DateTimeKind.Utc)
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
                      "situation": "TRAPPED",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 3, "child": 3, "elderly": 0 },
                      "medical_issues": ["PREGNANCY", "BREATHING_DIFFICULTY"],
                      "supplies": ["MEDICINE", "WATER"],
                      "additional_description": "Dang tru tren mai nha, 3 tre nho kho tho vi met moi, vo mang thai thang 8, nuoc van dang dang"
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
                WaitTimeMinutes = 35,
                Status = "Assigned",
                CreatedAt = new DateTime(2024, 10, 16, 8, 35, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 40, 0, DateTimeKind.Utc)
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
                Location = new Point(107.2870, 16.6340) { SRID = 4326 }, // Thôn Phong Mỹ, Phong Điền
                LocationAccuracy = 6,
                SosType = "RESCUE",
                OriginId = "D1B00003-0000-4A8A-B0BF-000000000003",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 5 | Bị thương: Người lớn 1: Gãy tay (Trung bình); Người lớn 2: Chảy máu đầu (Nghiêm trọng) | Ghi chú: Sat lo dat chan duong tinh lo, xe tai bi chan lai, can truc thang hoac di bo rung",
                StructuredData = """
                    {
                      "situation": "TRAPPED",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 5, "child": 0, "elderly": 0 },
                      "medical_issues": ["FRACTURE", "BLEEDING"],
                      "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                      "additional_description": "Sat lo dat chan duong tinh lo, 2 nguoi bi thuong nang, can truc thang hoac di bo rung"
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
                WaitTimeMinutes = 90,
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 8, 45, 0, DateTimeKind.Utc)
            },

            // B-2: Cầu bị ngập, cả thôn cô lập 3 ngày, thiếu sữa + thuốc — ~800m từ B-1
            // Người gửi: Lê Thị Hương (applicant3) — 0963333333
            new SosRequest
            {
                Id = 4,
                PacketId = Guid.Parse("B4000000-0000-0000-0000-000000000004"),
                ClusterId = 3,
                UserId = SeedConstants.Applicant3UserId,
                Location = new Point(107.2945, 16.6395) { SRID = 4326 }, // Thôn Hiền Lương, Phong Điền
                LocationAccuracy = 15,
                SosType = "RESCUE",
                OriginId = "D1B00004-0000-4A8A-B0BF-000000000004",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị cô lập | Số người: 120 | Trẻ em: 4 | Người già: 25 | Bị thương: Người lớn 1: Bệnh nền (Trung bình) | Ghi chú: Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be can sua gap, nguoi gia het thuoc huyet ap",
                StructuredData = """
                    {
                      "situation": "TRAPPED",
                      "can_move": false,
                      "has_injured": false,
                      "need_medical": true,
                      "others_are_stable": false,
                      "people_count": { "adult": 91, "child": 4, "elderly": 25 },
                      "medical_issues": ["CHRONIC_DISEASE"],
                      "supplies": ["FOOD", "WATER", "MEDICINE"],
                      "additional_description": "Ca thon co lap 3 ngay, het luong thuc va nuoc sach, 4 be duoi 1 tuoi can sua gap, nguoi gia het thuoc huyet ap"
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
                WaitTimeMinutes = 180,
                Status = "InProgress",
                CreatedAt = new DateTime(2024, 10, 16, 8, 50, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc)
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
                ClusterId = null,
                UserId = SeedConstants.Applicant4UserId,
                Location = new Point(108.0100, 16.0240) { SRID = 4326 }, // Thôn Phú Túc, Hòa Phú, Hòa Vang
                LocationAccuracy = 5,
                SosType = "RESCUE",
                OriginId = "D1C00005-0000-4A8A-B0BF-000000000005",
                RawMessage = "[CỨU HỘ] | Tình trạng: Bị thương | Số người: 1 | Bị thương: Người lớn 1: Gãy chân (Nghiêm trọng) | Ghi chú: Lac trong rung Hoa Phu, gay chan trai khong di duoc, dien thoai sap het pin 8%",
                StructuredData = """
                    {
                      "situation": "ISOLATED",
                      "can_move": false,
                      "has_injured": true,
                      "need_medical": true,
                      "others_are_stable": true,
                      "people_count": { "adult": 1, "child": 0, "elderly": 0 },
                      "medical_issues": ["FRACTURE"],
                      "supplies": ["MEDICINE", "RESCUE_EQUIPMENT"],
                      "additional_description": "Lac trong rung Hoa Phu tu sang, gay chan trai khong tu di duoc, dien thoai con 8% pin, toa do GPS 16.0240N 108.0100E"
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
                PriorityLevel = "Medium",
                WaitTimeMinutes = 0,
                Status = "Pending",
                CreatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc),
                LastUpdatedAt = new DateTime(2024, 10, 16, 9, 10, 0, DateTimeKind.Utc)
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
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "Critical",
                ConfidenceScore = 0.96,
                Explanation = "Phát hiện: người già liệt không tự di chuyển, nước vẫn dâng, không có thuyền. Nguy cơ chết đuối rất cao. Cần can thiệp ngay lập tức.",
                CreatedAt = now
            },
            new SosAiAnalysis
            {
                Id = 2,
                SosRequestId = 2,
                Metadata = "{\"urgency\": \"critical\", \"risk_factors\": [\"pregnant_woman\", \"young_children\", \"rooftop_refuge\"], \"needs\": [\"boat_rescue\", \"obstetrics_support\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "Critical",
                ConfidenceScore = 0.94,
                Explanation = "Phát hiện: phụ nữ mang thai tháng 8, 3 trẻ em từ 2-7 tuổi đang trú trên mái nhà. Rủi ro sinh non và chấn thương trẻ em rất cao.",
                CreatedAt = now.AddSeconds(30)
            },
            new SosAiAnalysis
            {
                Id = 3,
                SosRequestId = 3,
                Metadata = "{\"urgency\": \"high\", \"risk_factors\": [\"landslide\", \"bleeding_injury\", \"road_blocked\"], \"needs\": [\"medical_evacuation\", \"helicopter\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "High",
                ConfidenceScore = 0.89,
                Explanation = "Phát hiện: sạt lở đường, người bị thương chảy máu đầu (nguy cơ chấn thương sọ não). Đường bộ bị chặn hoàn toàn, cần trực thăng hoặc đường thủy.",
                CreatedAt = now.AddMinutes(1)
            },
            new SosAiAnalysis
            {
                Id = 4,
                SosRequestId = 4,
                Metadata = "{\"urgency\": \"high\", \"risk_factors\": [\"prolonged_isolation\", \"infant_malnutrition\", \"medication_shortage\"], \"needs\": [\"food_drop\", \"water\", \"medicine\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "High",
                ConfidenceScore = 0.91,
                Explanation = "Phát hiện: cô lập 3 ngày, 4 trẻ sơ sinh thiếu sữa, người cao tuổi hết thuốc huyết áp. Nguy cơ suy dinh dưỡng trẻ em và biến chứng tim mạch ở người già.",
                CreatedAt = now.AddMinutes(1).AddSeconds(30)
            },
            new SosAiAnalysis
            {
                Id = 5,
                SosRequestId = 5,
                Metadata = "{\"urgency\": \"medium\", \"risk_factors\": [\"isolated_forest\", \"broken_leg\", \"low_battery\"], \"needs\": [\"search_rescue\", \"medical\"]}",
                ModelVersion = "v1.0",
                SuggestedSeverityLevel = "Moderate",
                ConfidenceScore = 0.85,
                Explanation = "Phát hiện: người một mình bị gãy chân trong rừng sâu, điện thoại sắp hết pin. Vị trí GPS xác định. Ưu tiên triển khai đội tìm kiếm cứu nạn rừng núi.",
                CreatedAt = now.AddMinutes(2)
            }
        );
    }
}
