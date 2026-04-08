using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class SystemSeeder
{
    public static void SeedSystem(this ModelBuilder modelBuilder)
    {
        SeedNotifications(modelBuilder);
        SeedPrompts(modelBuilder);
        SeedRescuerScoreVisibilityConfig(modelBuilder);
        SeedServiceZone(modelBuilder);
      SeedSosClusterGroupingConfig(modelBuilder);
        SeedSosPriorityRuleConfig(modelBuilder);
    }

    private static void SeedNotifications(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Notification>().HasData(
            new Notification
            {
                Id = 1,
                Content = "Có yêu cầu cứu hộ mới cần xử lý",
                CreatedAt = now
            },
            new Notification
            {
                Id = 2,
                Content = "Nhiệm vụ #1 đã được giao cho đội của bạn",
                CreatedAt = now
            }
        );
    }

    private static void SeedPrompts(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Prompt>().HasData(
            new Prompt
            {
                Id = 1,
                Name = "SOS Analysis Prompt",
                PromptType = "SosPriorityAnalysis",
                Purpose = "Phân tích tin nhắn SOS để trích xuất thông tin",
                SystemPrompt = "Bạn là một AI chuyên phân tích các tin nhắn cầu cứu trong thiên tai...",
                Temperature = 0.3,
                MaxTokens = 1000,
                Version = "v1.0",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = "AIzaSyBc5pvH7EpouC886yAEJUkKmH5bXev3gMM",
                Model = "gemini-2.5-flash",
                IsActive = false, // Đã được thay thế bởi prompt Id=3
                CreatedAt = now
            },
            new Prompt
            {
                Id = 2,
                Name = "Mission Planning Prompt",
                PromptType = "MissionPlanning",
                Purpose = "Lập kế hoạch nhiệm vụ cứu trợ",
                SystemPrompt = @"Bạn là một điều phối viên cứu hộ thực địa. Nhiệm vụ của bạn là lập kế hoạch các BƯỚC DI CHUYỂN VÀ HÀNH ĐỘNG CỤ THỂ cho đội cứu hộ ngoài thực địa — giống như lệnh điều phối từng bước một.

Mỗi activity = một hành động vật lý cụ thể mà đội cứu hộ thực sự thực hiện theo thứ tự. Không phải chiến lược, không phải đánh giá — là hành động thực tế.

═══════════════════════════════════════════════════
CÁC LOẠI ACTIVITY HỢP LỆ VÀ Ý NGHĨA
═══════════════════════════════════════════════════

COLLECT_SUPPLIES — Di chuyển đến kho, lấy vật tư:
  → Khi nào dùng: LUÔN LUÔN trước bất kỳ DELIVER_SUPPLIES nào. Không có COLLECT thì không có DELIVER.
  → Điền bắt buộc: sos_request_id (ID của SOS request được phục vụ), depot_id, depot_name, depot_address, supplies_to_collect (từng mặt hàng với item_id đúng theo danh sách kho, số lượng, đơn vị).
  → Chỉ lấy vật tư kho ĐANG có sẵn (so_luong_kha_dung > 0). Thiếu gì → ghi vào special_notes.
  → description mẫu: ""Di chuyển đến kho [tên] tại [địa chỉ]. Lấy: [vật tư A] x[sl] [đv], [vật tư B] x[sl] [đv].""

DELIVER_SUPPLIES — Di chuyển đến nạn nhân, giao vật tư (đã lấy từ bước COLLECT trước):
  → Điền: sos_request_id (ID của SOS được giao hàng), depot_id/depot_name/depot_address của kho nguồn, supplies_to_collect (có item_id) = vật tư đang giao.
  → description mẫu: ""Di chuyển đến [địa điểm nạn nhân]. Giao vật tư (lấy từ kho [tên]): [vật tư A] x[sl] [đv] cho [đối tượng].""

RESCUE — Di chuyển đến hiện trường, thực hiện cứu người:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Di chuyển đến [tọa độ/địa điểm]. Thực hiện [hành động cụ thể: kéo người khỏi đống đổ nát / cứu người khỏi lũ / ...].""

EVACUATE — Di chuyển đưa người ra khỏi vùng nguy hiểm đến nơi an toàn:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Đưa [số người] từ [điểm xuất phát] đến [điểm an toàn] bằng [phương tiện].""

MEDICAL_AID — Sơ cứu/chăm sóc y tế tại chỗ không cần di chuyển xa:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Thực hiện sơ cứu tại [địa điểm] cho [số người]: [hành động y tế cụ thể].""

═══════════════════════════════════════════════════
QUY TẮC CỐT LÕI — KHÔNG ĐƯỢC VI PHẠM
═══════════════════════════════════════════════════

1. KHÔNG CÓ BƯỚC ""ĐÁNH GIÁ"" — Đội cứu hộ hành động ngay, không có step nào chỉ để đánh giá.
2. COLLECT_SUPPLIES TRƯỚC DELIVER_SUPPLIES — Không thể giao vật tư chưa lấy.
3. FOOD, WATER, MEDICAL_KIT, thuốc, sữa, lương thực → PHẢI là supplies_to_collect trong COLLECT_SUPPLIES. KHÔNG vào mảng resources.
4. resources[] = CHỈ ĐƯỢC CHỨA: TEAM, VEHICLE, BOAT, EQUIPMENT (công cụ/phương tiện). Tuyệt đối không có FOOD/WATER/MEDICAL_KIT trong resources.
5. Mỗi bước mô tả ĐI ĐÂU và LÀM GÌ cụ thể.

═══════════════════════════════════════════════════
VÍ DỤ ĐÚNG về thứ tự activities:
  Bước 1: COLLECT_SUPPLIES — Di chuyển đến Kho A, lấy 50kg gạo + 200 chai nước.
  Bước 2: DELIVER_SUPPLIES — Di chuyển đến tọa độ X, giao 50kg gạo + 200 chai nước (từ Kho A) cho 120 nạn nhân.
  Bước 3: RESCUE — Di chuyển đến tọa độ Y, kéo 5 người khỏi đống đổ nát sạt lở.
  Bước 4: EVACUATE — Đưa 2 người bị thương nặng từ tọa độ Y về bệnh viện bằng trực thăng.
  Bước 5: MEDICAL_AID — Sơ cứu băng bó vết thương tại hiện trường tọa độ Y.

═══════════════════════════════════════════════════
FORMAT JSON PHẢN HỒI (chỉ trả về JSON, không giải thích thêm)
═══════════════════════════════════════════════════

{
  ""mission_title"": ""Tên nhiệm vụ ngắn gọn"",
  ""mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""priority_score"": 0.0-10.0,
  ""severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Tóm tắt tình hình và tổng nhu cầu vật tư (liệt kê từng loại và số lượng cần)"",
  ""activities"": [
    {
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES"",
      ""description"": ""Di chuyển đến kho [tên kho] tại [địa chỉ]. Lấy: [vật tư A] x[sl] [đv], [vật tư B] x[sl] [đv]."",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Tên kho thực tế"",
      ""depot_address"": ""Địa chỉ kho thực tế"",
      ""supplies_to_collect"": [
        { ""item_id"": 1, ""item_name"": ""Gạo"", ""quantity"": 50, ""unit"": ""kg"" }
      ],
      ""priority"": ""Critical"",
      ""estimated_time"": ""30 phút"",
      ""suggested_team"": { ""team_id"": 5, ""team_name"": ""Đội A"", ""team_type"": ""RescueTeam"", ""reason"": ""Gần nhất"", ""assembly_point_name"": ""Trụ sở A"", ""latitude"": 16.46, ""longitude"": 107.59 }
    },
    {
      ""step"": 2,
      ""activity_type"": ""DELIVER_SUPPLIES"",
      ""description"": ""Di chuyển đến [địa điểm nạn nhân]. Giao (từ kho [tên]): [vật tư A] x[sl] [đv] cho [mô tả đối tượng]."",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Tên kho nguồn"",
      ""depot_address"": ""Địa chỉ kho nguồn"",
      ""supplies_to_collect"": [
        { ""item_id"": 1, ""item_name"": ""Gạo"", ""quantity"": 50, ""unit"": ""kg"" }
      ],
      ""priority"": ""Critical"",
      ""estimated_time"": ""1 giờ"",
      ""suggested_team"": { ""team_id"": 5, ""team_name"": ""Đội A"", ""team_type"": ""RescueTeam"", ""reason"": ""Gần nhất"", ""assembly_point_name"": ""Trụ sở A"", ""latitude"": 16.46, ""longitude"": 107.59 }
    },
    {
      ""step"": 3,
      ""activity_type"": ""RESCUE"",
      ""description"": ""Di chuyển đến [tọa độ/địa điểm]. [Hành động cứu hộ cụ thể]."",
      ""sos_request_id"": 2,
      ""depot_id"": null,
      ""depot_name"": null,
      ""depot_address"": null,
      ""supplies_to_collect"": null,
      ""priority"": ""Critical"",
      ""estimated_time"": ""2 giờ"",
      ""suggested_team"": { ""team_id"": 6, ""team_name"": ""Đội B"", ""team_type"": ""MedicalTeam"", ""reason"": ""Có y tế"", ""assembly_point_name"": ""Trụ sở B"", ""latitude"": 16.50, ""longitude"": 107.55 }
    }
  ],
  ""resources"": [
    { ""resource_type"": ""TEAM"", ""description"": ""Đội cứu hộ chuyên nghiệp"", ""quantity"": 2, ""priority"": ""Critical"" },
    { ""resource_type"": ""VEHICLE"", ""description"": ""Trực thăng cứu hộ"", ""quantity"": 1, ""priority"": ""Critical"" }
  ],
  ""estimated_duration"": ""X giờ"",
  ""special_notes"": ""Vật tư kho không có sẵn / điều kiện đặc biệt hiện trường"",
  ""confidence_score"": 0.85
}",
                UserPromptTemplate = @"Lập kế hoạch nhiệm vụ cứu hộ cho các SOS sau:

{{sos_requests_data}}

Tổng số SOS: {{total_count}}

--- KHO TIẾP TẾ KHẢ DỤNG GẦN KHU VỰC ---
{{depots_data}}

QUAN TRỌNG — LÀM THEO ĐÚNG THỨ TỰ NÀY:
1. Xác định tổng vật tư cần thiết từ tất cả SOS.
2. Đối chiếu với kho: vật tư nào kho có (so_luong_kha_dung > 0) → tạo bước COLLECT_SUPPLIES (step 1) lấy từ kho đó.
3. Tiếp theo tạo bước DELIVER_SUPPLIES (step 2) giao vật tư vừa lấy đến nạn nhân.
4. Thêm các bước RESCUE / EVACUATE / MEDICAL_AID cho hành động cứu hộ trực tiếp.
5. Vật tư kho không có → ghi thiếu hụt vào special_notes (KHÔNG đặt vào resources).
6. resources[] = chỉ TEAM, VEHICLE, BOAT, EQUIPMENT.

Trả về JSON (không giải thích, không markdown).",
                Temperature = 0.5,
                MaxTokens = 4096,
                Version = "v1.0",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = "AIzaSyDc4rHO4Vlfwp4BP3WP8BLc7x90q5j-ddk",
                Model = "gemini-2.5-flash",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 3,
                Name = "SOS_PRIORITY_ANALYSIS",
                PromptType = "SosPriorityAnalysis",
                Purpose = "Phân tích yêu cầu SOS để xác định mức độ ưu tiên và nghiêm trọng",
                SystemPrompt = @"Bạn là một chuyên gia phân tích tình huống khẩn cấp. Nhiệm vụ của bạn là phân tích các yêu cầu SOS và đánh giá mức độ ưu tiên.

Các mức độ ưu tiên:
- Critical: Tình huống đe dọa tính mạng, cần can thiệp ngay lập tức (người bị thương nặng, ngập nước sâu, cháy, sập nhà)
- High: Tình huống nghiêm trọng cần hỗ trợ khẩn cấp trong vài giờ (có người bị thương nhẹ, thiếu nước/thức ăn, mắc kẹt)
- Medium: Cần hỗ trợ nhưng không nguy hiểm tức thì (cần di dời, cần vật tư, cần thông tin)
- Low: Yêu cầu hỗ trợ không khẩn cấp

Các mức độ nghiêm trọng:
- Critical: Đe dọa tính mạng trực tiếp
- Severe: Nguy hiểm cao nhưng chưa đe dọa tính mạng ngay
- Moderate: Tình huống khó khăn cần hỗ trợ
- Minor: Tình huống ít nghiêm trọng

Bạn phải trả lời bằng JSON với format sau:
{
  ""priority"": ""Critical|High|Medium|Low"",
  ""severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""explanation"": ""Giải thích ngắn gọn lý do đánh giá"",
  ""confidence_score"": 0.0-1.0
}",
                UserPromptTemplate = @"Phân tích yêu cầu SOS sau:

Loại SOS: {{sos_type}}
Tin nhắn: {{raw_message}}
Dữ liệu chi tiết: {{structured_data}}

Hãy đánh giá mức độ ưu tiên và nghiêm trọng của yêu cầu này.",
                Model = "gemini-2.5-flash",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = "AIzaSyDc4rHO4Vlfwp4BP3WP8BLc7x90q5j-ddk",
                Temperature = 0.3,
                MaxTokens = 1024,
                Version = "1.0",
                IsActive = true,
                CreatedAt = now
            }
        );
    }

    private static void SeedServiceZone(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Polygon bao phủ Miền Trung Việt Nam (Thanh Hoá → Bình Thuận + Tây Nguyên)
        // theo thứ tự: SW → NW → NE → SE (đóng lại ở SW)
        var defaultCoords = new[]
        {
            new { latitude = 10.3, longitude = 103.0 },
            new { latitude = 20.5, longitude = 103.0 },
            new { latitude = 20.5, longitude = 109.5 },
            new { latitude = 10.3, longitude = 109.5 }
        };

        modelBuilder.Entity<ServiceZone>().HasData(
            new ServiceZone
            {
                Id = 1,
                Name = "Vùng phục vụ Miền Trung Việt Nam",
                CoordinatesJson = JsonSerializer.Serialize(defaultCoords),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }

    private static void SeedRescuerScoreVisibilityConfig(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RescuerScoreVisibilityConfig>().HasData(
            new RescuerScoreVisibilityConfig
            {
                Id = 1,
                MinimumEvaluationCount = 0,
                UpdatedBy = null,
                UpdatedAt = now
            }
        );
    }

          private static void SeedSosClusterGroupingConfig(ModelBuilder modelBuilder)
          {
            var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<SosClusterGroupingConfig>().HasData(
              new SosClusterGroupingConfig
              {
                Id = 1,
                MaximumDistanceKm = 10.0,
                UpdatedBy = null,
                UpdatedAt = now
              }
            );
          }

    private static void SeedSosPriorityRuleConfig(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

      var configModel = new SosPriorityRuleConfigModel
      {
        Id = 1,
        UpdatedAt = now
      };
      SosPriorityRuleConfigSupport.SyncLegacyFields(configModel, new SosPriorityRuleConfigDocument());

        modelBuilder.Entity<SosPriorityRuleConfig>().HasData(
            new SosPriorityRuleConfig
            {
                Id = 1,
          ConfigJson = configModel.ConfigJson,
          IssueWeightsJson = configModel.IssueWeightsJson,
          MedicalSevereIssuesJson = configModel.MedicalSevereIssuesJson,
          AgeWeightsJson = configModel.AgeWeightsJson,
          RequestTypeScoresJson = configModel.RequestTypeScoresJson,
          SituationMultipliersJson = configModel.SituationMultipliersJson,
          PriorityThresholdsJson = configModel.PriorityThresholdsJson,
                UpdatedAt = now
            }
        );
    }
}

