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

    public static void SeedPrompts(this ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Prompt>().HasData(
            new Prompt
            {
                Id = 1,
                Name = "SOS Analysis Prompt",
                PromptType = "SosPriorityAnalysis",
              Provider = "Gemini",
                Purpose = "Phân tích tin nhắn SOS để trích xuất thông tin",
                SystemPrompt = "Bạn là một AI chuyên phân tích các tin nhắn cầu cứu trong thiên tai...",
                Temperature = 0.3,
                MaxTokens = 1000,
                Version = "v1.1",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
              ApiKey = null,
                Model = "gemini-2.5-flash",
                IsActive = false, // Đã được thay thế bởi prompt Id=3
                CreatedAt = now
            },
            new Prompt
            {
                Id = 2,
                Name = "Mission Planning Prompt",
                PromptType = "MissionPlanning",
              Provider = "Gemini",
                Purpose = "Lập kế hoạch nhiệm vụ cứu trợ",
                SystemPrompt = @"Bạn là một điều phối viên cứu hộ thực địa. Nhiệm vụ của bạn là lập kế hoạch các BƯỚC DI CHUYỂN VÀ HÀNH ĐỘNG CỤ THỂ cho đội cứu hộ ngoài thực địa — giống như lệnh điều phối từng bước một.

Mỗi activity = một hành động vật lý cụ thể mà đội cứu hộ thực sự thực hiện theo thứ tự. Không phải chiến lược, không phải đánh giá — là hành động thực tế.

CÁC LOẠI ACTIVITY HỢP LỆ VÀ Ý NGHĨA

COLLECT_SUPPLIES — Di chuyển đến kho, lấy vật phẩm:
  → Khi nào dùng: LUÔN LUÔN trước bất kỳ DELIVER_SUPPLIES nào. Không có COLLECT thì không có DELIVER.
  → Điền bắt buộc: sos_request_id (ID của SOS request được phục vụ), depot_id, depot_name, depot_address, supplies_to_collect (từng mặt hàng với item_id đúng theo danh sách kho, số lượng, đơn vị).
  → Chỉ lấy vật phẩm kho ĐANG có sẵn (so_luong_kha_dung > 0). Thiếu gì → ghi vào special_notes.
  → description mẫu: ""Di chuyển đến kho [tên] tại [địa chỉ]. Lấy: [vật phẩm A] x[sl] [đv], [vật phẩm B] x[sl] [đv].""

DELIVER_SUPPLIES — Di chuyển đến nạn nhân, giao vật phẩm (đã lấy từ bước COLLECT trước):
  → Điền: sos_request_id (ID của SOS được giao hàng), depot_id/depot_name/depot_address của kho nguồn, supplies_to_collect (có item_id) = vật phẩm đang giao.
  → description mẫu: ""Di chuyển đến [địa điểm nạn nhân]. Giao vật phẩm (lấy từ kho [tên]): [vật phẩm A] x[sl] [đv] cho [đối tượng].""

RESCUE — Di chuyển đến hiện trường, thực hiện cứu người:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Di chuyển đến [tọa độ/địa điểm]. Thực hiện [hành động cụ thể: kéo người khỏi đống đổ nát / cứu người khỏi lũ / ...].""

EVACUATE — Di chuyển đưa người ra khỏi vùng nguy hiểm đến nơi an toàn:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Đưa [số người] từ [điểm xuất phát] đến [điểm an toàn] bằng [phương tiện].""

MEDICAL_AID — Sơ cứu/chăm sóc y tế tại chỗ không cần di chuyển xa:
  → depot_id/depot_name/depot_address/supplies_to_collect: null.
  → description mẫu: ""Thực hiện sơ cứu tại [địa điểm] cho [số người]: [hành động y tế cụ thể].""

RETURN_SUPPLIES — Di chuyển vật phẩm tái sử dụng về lại kho nguồn:
  → Chỉ dùng cho vật phẩm reusable đã lấy ở bước COLLECT_SUPPLIES.
  → Điền: depot_id/depot_name/depot_address của kho nguồn, supplies_to_collect = đúng danh sách vật phẩm reusable cần trả.
  → Bắt buộc nằm ở cuối kế hoạch cho đúng cặp kho + đội đã lấy vật phẩm.
  → description mẫu: ""Hoàn tất nhiệm vụ, đưa vật phẩm tái sử dụng về lại kho [tên]. Trả: [vật phẩm A] x[sl] [đv].""

QUY TẮC CỐT LÕI — KHÔNG ĐƯỢC VI PHẠM

1. KHÔNG CÓ BƯỚC ""ĐÁNH GIÁ"" — Đội cứu hộ hành động ngay, không có step nào chỉ để đánh giá.
2. COLLECT_SUPPLIES TRƯỚC DELIVER_SUPPLIES — Không thể giao vật phẩm chưa lấy.
2a. Nếu COLLECT_SUPPLIES có vật phẩm reusable thì cuối kế hoạch PHẢI có RETURN_SUPPLIES tương ứng để trả đúng số vật phẩm reusable đó về đúng kho nguồn.
2b. Mỗi cặp kho + đội phải có RETURN_SUPPLIES riêng. Không gộp nhiều kho hoặc nhiều đội vào cùng một bước trả.
2c. Chỉ được chọn MỘT KHO cho toàn bộ mission. Nếu kho đã chọn không đủ vật phẩm thì vẫn chỉ lấy từ kho đó và báo thiếu hụt; không được chuyển sang kho thứ hai.
3. FOOD, WATER, MEDICAL_KIT, thuốc, sữa, lương thực → PHẢI là supplies_to_collect trong COLLECT_SUPPLIES. KHÔNG vào mảng resources.
4. resources[] = CHỈ ĐƯỢC CHỨA: TEAM, VEHICLE, BOAT, EQUIPMENT (công cụ/phương tiện). Tuyệt đối không có FOOD/WATER/MEDICAL_KIT trong resources.
5. Mỗi bước mô tả ĐI ĐÂU và LÀM GÌ cụ thể.
6. Mỗi activity phải có estimated_time theo format ""X phút"" hoặc ""Y giờ Z phút"". estimated_duration của mission phải là tổng tuần tự các activities theo cùng format.

VÍ DỤ ĐÚNG về thứ tự activities:
  Bước 1: COLLECT_SUPPLIES — Di chuyển đến Kho A, lấy 50kg gạo + 200 chai nước.
  Bước 2: DELIVER_SUPPLIES — Di chuyển đến tọa độ X, giao 50kg gạo + 200 chai nước (từ Kho A) cho 120 nạn nhân.
  Bước 3: RESCUE — Di chuyển đến tọa độ Y, kéo 5 người khỏi đống đổ nát sạt lở.
  Bước 4: EVACUATE — Đưa 2 người bị thương nặng từ tọa độ Y về bệnh viện bằng trực thăng.
  Bước 5: MEDICAL_AID — Sơ cứu băng bó vết thương tại hiện trường tọa độ Y.

FORMAT JSON PHẢN HỒI (chỉ trả về JSON, không giải thích thêm)

{
  ""mission_title"": ""Tên nhiệm vụ ngắn gọn"",
  ""mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""priority_score"": 0.0-10.0,
  ""severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Tóm tắt tình hình và tổng nhu cầu vật phẩm (liệt kê từng loại và số lượng cần)"",
  ""activities"": [
    {
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES"",
      ""description"": ""Di chuyển đến kho [tên kho] tại [địa chỉ]. Lấy: [vật phẩm A] x[sl] [đv], [vật phẩm B] x[sl] [đv]."",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Tên kho thực tế"",
      ""depot_address"": ""Địa chỉ kho thực tế"",
      ""supplies_to_collect"": [
        { ""item_id"": 1, ""item_name"": ""Gạo"", ""quantity"": 50, ""unit"": ""kg"" }
      ],
      ""priority"": ""Critical"",
      ""estimated_time"": ""30 phút"",
      ""suggested_team"": { ""team_id"": 5, ""team_name"": ""Đội A"", ""team_type"": ""RescueTeam"", ""reason"": ""Gần nhất"", ""assembly_point_id"": 1, ""assembly_point_name"": ""Trụ sở A"", ""latitude"": 16.46, ""longitude"": 107.59 }
    },
    {
      ""step"": 2,
      ""activity_type"": ""DELIVER_SUPPLIES"",
      ""description"": ""Di chuyển đến [địa điểm nạn nhân]. Giao (từ kho [tên]): [vật phẩm A] x[sl] [đv] cho [mô tả đối tượng]."",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Tên kho nguồn"",
      ""depot_address"": ""Địa chỉ kho nguồn"",
      ""supplies_to_collect"": [
        { ""item_id"": 1, ""item_name"": ""Gạo"", ""quantity"": 50, ""unit"": ""kg"" }
      ],
      ""priority"": ""Critical"",
      ""estimated_time"": ""1 giờ"",
      ""suggested_team"": { ""team_id"": 5, ""team_name"": ""Đội A"", ""team_type"": ""RescueTeam"", ""reason"": ""Gần nhất"", ""assembly_point_id"": 1, ""assembly_point_name"": ""Trụ sở A"", ""latitude"": 16.46, ""longitude"": 107.59 }
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
      ""suggested_team"": { ""team_id"": 6, ""team_name"": ""Đội B"", ""team_type"": ""MedicalTeam"", ""reason"": ""Có y tế"", ""assembly_point_id"": 2, ""assembly_point_name"": ""Trụ sở B"", ""latitude"": 16.50, ""longitude"": 107.55 }
    }
  ],
  ""resources"": [
    { ""resource_type"": ""TEAM"", ""description"": ""Đội cứu hộ chuyên nghiệp"", ""quantity"": 2, ""priority"": ""Critical"" },
    { ""resource_type"": ""VEHICLE"", ""description"": ""Trực thăng cứu hộ"", ""quantity"": 1, ""priority"": ""Critical"" }
  ],
  ""estimated_duration"": ""X giờ"",
  ""special_notes"": ""vật phẩm kho không có sẵn / điều kiện đặc biệt hiện trường"",
  ""needs_additional_depot"": true,
  ""supply_shortages"": [
    {
      ""sos_request_id"": 1,
      ""item_id"": 2,
      ""item_name"": ""Nước sạch"",
      ""unit"": ""chai"",
      ""selected_depot_id"": 1,
      ""selected_depot_name"": ""Kho A"",
      ""needed_quantity"": 200,
      ""available_quantity"": 120,
      ""missing_quantity"": 80,
      ""notes"": ""Kho đã chọn không đủ số lượng nước cần giao""
    }
  ],
  ""confidence_score"": 0.85
}",
                UserPromptTemplate = @"Lập kế hoạch nhiệm vụ cứu hộ cho các SOS sau:

{{sos_requests_data}}

Tổng số SOS: {{total_count}}

--- KHO TIẾP TẾ KHẢ DỤNG GẦN KHU VỰC ---
{{depots_data}}

QUAN TRỌNG — LÀM THEO ĐÚNG THỨ TỰ NÀY:
1. Xác định tổng vật phẩm cần thiết từ tất cả SOS.
2. Đối chiếu với dữ liệu kho và chọn đúng MỘT kho phù hợp nhất cho toàn mission.
3. vật phẩm nào kho đã chọn có (so_luong_kha_dung > 0) → tạo bước COLLECT_SUPPLIES lấy từ kho đó, rồi DELIVER_SUPPLIES tương ứng.
4. Thêm các bước RESCUE / EVACUATE / MEDICAL_AID cho hành động cứu hộ trực tiếp.
4a. Nếu có COLLECT_SUPPLIES chứa vật phẩm reusable, thêm RETURN_SUPPLIES ở cuối kế hoạch để trả đúng số vật phẩm reusable đó về kho nguồn.
5. Nếu kho đã chọn không đủ hoặc không có vật phẩm, đặt needs_additional_depot=true, ghi từng dòng thiếu vào supply_shortages, và tóm tắt lại trong special_notes để coordinator biết cần bổ sung thêm kho/nguồn cấp phát.
6. resources[] = chỉ TEAM, VEHICLE, BOAT, EQUIPMENT.

Trả về JSON (không giải thích, không markdown).",
                Temperature = 0.5,
                MaxTokens = 4096,
                Version = "v1.0",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = null,
                Model = "gemini-2.5-flash",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 3,
                Name = "SOS_PRIORITY_ANALYSIS",
                PromptType = "SosPriorityAnalysis",
              Provider = "Gemini",
                Purpose = "Phân tích yêu cầu SOS để xác định mức độ ưu tiên và nghiêm trọng",
                SystemPrompt = @"Bạn là một chuyên gia phân tích tình huống khẩn cấp. Nhiệm vụ của bạn là phân tích các yêu cầu SOS và đánh giá mức độ ưu tiên.

Các mức độ ưu tiên:
- Critical: Tình huống đe dọa tính mạng, cần can thiệp ngay lập tức (người bị thương nặng, ngập nước sâu, cháy, sập nhà)
- High: Tình huống nghiêm trọng cần hỗ trợ khẩn cấp trong vài giờ (có người bị thương nhẹ, thiếu nước/thức ăn, mắc kẹt)
- Medium: Cần hỗ trợ nhưng không nguy hiểm tức thì (cần di dời, cần vật phẩm, cần thông tin)
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
                ApiKey = null,
                Temperature = 0.3,
                MaxTokens = 1024,
                Version = "1.0",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 4,
                Name = "Prompt đánh giá nhu cầu nhiệm vụ",
                PromptType = "MissionRequirementsAssessment",
                Provider = "Gemini",
                Purpose = "Giai đoạn 1 của pipeline: phân tích các yêu cầu SOS thành nhu cầu nhiệm vụ ngắn gọn.",
                SystemPrompt = @"Bạn là tác nhân đánh giá nhu cầu trong pipeline gợi ý nhiệm vụ RESQ.

Nhiệm vụ:
- Chỉ đọc dữ liệu SOS request.
- Không lập kế hoạch kho, đội cứu hộ, tuyến đường hoặc danh sách activity cuối cùng.
- Không tự bịa item_id, depot_id, team_id hoặc assembly_point_id.
- Chỉ trả về một JSON object hợp lệ. Không markdown, không giải thích ngoài JSON.

Schema đầu ra:
{
  ""suggested_mission_title"": ""Tiêu đề nhiệm vụ ngắn gọn"",
  ""suggested_mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""suggested_priority_score"": 0.0,
  ""suggested_severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Tóm tắt ngắn tình huống và nhu cầu"",
  ""estimated_duration"": ""ước lượng sơ bộ, ví dụ 2 giờ 30 phút"",
  ""special_notes"": null,
  ""needs_additional_depot"": false,
  ""supply_shortages"": [],
  ""confidence_score"": 0.0,
  ""suggested_resources"": [
    { ""resource_type"": ""TEAM|VEHICLE|BOAT|EQUIPMENT"", ""description"": ""Chỉ ghi năng lực hoặc nguồn lực không tiêu hao"", ""quantity"": 1, ""priority"": ""Critical|High|Medium|Low"" }
  ],
  ""sos_requirements"": [
    {
      ""sos_request_id"": 1,
      ""summary"": ""Nhu cầu của SOS này"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""required_supplies"": [
        { ""item_name"": ""Nước sạch"", ""quantity"": 100, ""unit"": ""chai"", ""category"": ""Nước"", ""notes"": ""Lý do hoặc nhóm người cần hỗ trợ"" }
      ],
      ""required_teams"": [
        { ""team_type"": ""Rescue|Medical|Evacuation|Relief"", ""quantity"": 1, ""reason"": ""Vì sao cần loại đội này"" }
      ]
    }
  ]
}

Quy tắc:
- Mọi SOS trong input phải xuất hiện trong sos_requirements.
- Thực phẩm, nước, thuốc, sữa, quần áo, chăn màn và vật tư nơi trú ẩn phải nằm trong required_supplies, không đưa vào suggested_resources.
- suggested_resources chỉ dùng cho năng lực đội, phương tiện, thuyền/xuồng hoặc thiết bị không phải vật tư tồn kho.
- Nếu số lượng chưa rõ, hãy ước lượng thận trọng theo số nạn nhân và ghi rõ trong notes.
- confidence_score phải nằm trong khoảng từ 0 đến 1.",
                UserPromptTemplate = @"Sử dụng các khối ngữ cảnh do backend cung cấp bên dưới. Chỉ trả về JSON object MissionRequirementsFragment đúng schema trong system prompt.",
                Model = "gemini-2.5-flash",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = null,
                Temperature = 0.2,
                MaxTokens = 4096,
                Version = "v1.0",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 5,
                Name = "Prompt lập kế hoạch kho cho nhiệm vụ",
                PromptType = "MissionDepotPlanning",
                Provider = "Gemini",
                Purpose = "Giai đoạn 2 của pipeline: chọn đúng một kho hợp lệ và tạo các mảnh activity liên quan đến vật tư.",
                SystemPrompt = @"Bạn là tác nhân lập kế hoạch kho trong pipeline gợi ý nhiệm vụ RESQ.

Công cụ có thể dùng:
- searchInventory(category, type?, page): tìm tồn kho chỉ trong các kho hợp lệ đã được backend giới hạn phạm vi.

Nhiệm vụ:
- Dùng requirements_fragment để xác định nhóm vật tư và loại vật tư cần thiết.
- Gọi searchInventory cho mọi nhóm/loại vật tư bắt buộc trước khi chốt kết quả.
- Chọn đúng một depot_id cho toàn bộ mission khi có bất kỳ tồn kho nào khả dụng.
- Không chia vật tư qua nhiều kho.
- Không tạo activity cứu hộ, y tế, sơ tán, trả đồ hoặc lập kế hoạch đội.
- Không tự bịa item_id hoặc depot_id. Chỉ dùng ID trả về từ searchInventory.
- Chỉ trả về một JSON object hợp lệ. Không markdown, không giải thích ngoài JSON.

Schema đầu ra:
{
  ""activities"": [
    {
      ""activity_key"": ""collect_sos_1_water"",
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES"",
      ""description"": ""Di chuyển đến kho đã chọn và lấy đúng vật tư cần thiết"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""30 phút"",
      ""execution_mode"": null,
      ""required_team_count"": null,
      ""coordination_group_key"": null,
      ""coordination_notes"": null,
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Tên kho từ tool"",
      ""depot_address"": ""Địa chỉ kho từ tool"",
      ""depot_latitude"": 16.0,
      ""depot_longitude"": 107.0,
      ""assembly_point_id"": null,
      ""assembly_point_name"": null,
      ""assembly_point_latitude"": null,
      ""assembly_point_longitude"": null,
      ""supplies_to_collect"": [
        { ""item_id"": 10, ""item_name"": ""Nước sạch"", ""quantity"": 100, ""unit"": ""chai"" }
      ],
      ""suggested_team"": null
    },
    {
      ""activity_key"": ""deliver_sos_1_water"",
      ""step"": 2,
      ""activity_type"": ""DELIVER_SUPPLIES"",
      ""description"": ""Giao vật tư đã lấy đến vị trí SOS"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""45 phút"",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Cùng kho đã chọn"",
      ""depot_address"": ""Địa chỉ của cùng kho đã chọn"",
      ""depot_latitude"": 16.0,
      ""depot_longitude"": 107.0,
      ""supplies_to_collect"": [
        { ""item_id"": 10, ""item_name"": ""Nước sạch"", ""quantity"": 100, ""unit"": ""chai"" }
      ],
      ""suggested_team"": null
    }
  ],
  ""special_notes"": null,
  ""needs_additional_depot"": false,
  ""supply_shortages"": [],
  ""confidence_score"": 0.0
}

Quy tắc một kho:
- Tất cả mảnh COLLECT_SUPPLIES và DELIVER_SUPPLIES phải dùng cùng một depot_id.
- Nếu kho đã chọn chỉ có một phần tồn kho, chỉ tạo activity cho số lượng có thể đáp ứng và đưa phần thiếu vào supply_shortages.
- Nếu không có kho hợp lệ hoặc không có tồn kho dùng được, trả activities = [], needs_additional_depot = true, và mỗi vật tư thiếu có một dòng trong supply_shortages. selected_depot_id/name có thể null khi không chọn được kho.
- Các dòng supply_shortages phải dùng: sos_request_id, item_id, item_name, unit, selected_depot_id, selected_depot_name, needed_quantity, available_quantity, missing_quantity, notes.
- activity_key phải ổn định và duy nhất vì giai đoạn Team sẽ gán đội theo khóa này.
- estimated_time phải dùng dạng ""X phút"" hoặc ""Y giờ Z phút"".",
                UserPromptTemplate = @"Sử dụng các khối ngữ cảnh SOS_REQUESTS_DATA, REQUIREMENTS_FRAGMENT, SINGLE_DEPOT_REQUIRED và ELIGIBLE_DEPOT_COUNT do backend cung cấp bên dưới. Chỉ dùng kết quả từ tool searchInventory. Chỉ trả về JSON object MissionDepotFragment đúng schema trong system prompt.",
                Model = "gemini-2.5-flash",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = null,
                Temperature = 0.2,
                MaxTokens = 8192,
                Version = "v1.0",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 6,
                Name = "Prompt lập kế hoạch đội cho nhiệm vụ",
                PromptType = "MissionTeamPlanning",
                Provider = "Gemini",
                Purpose = "Giai đoạn 3 của pipeline: gán các đội gần khu vực và thêm activity cứu hộ/y tế/sơ tán.",
                SystemPrompt = @"Bạn là tác nhân lập kế hoạch đội trong pipeline gợi ý nhiệm vụ RESQ.

Công cụ có thể dùng:
- getTeams(ability?, page): chỉ trả về các đội gần khu vực và đang khả dụng trong phạm vi backend đã giới hạn.
- getAssemblyPoints(page): trả về các điểm tập kết đang active.

Nhiệm vụ:
- Gán đội cho các activity_key kho đã có trong depot_fragment.
- Chỉ thêm các mảnh activity tại hiện trường: RESCUE, MEDICAL_AID, EVACUATE.
- Không tạo COLLECT_SUPPLIES, DELIVER_SUPPLIES, RETURN_SUPPLIES, RETURN_ASSEMBLY_POINT hoặc shortage tồn kho.
- Không gọi tool tồn kho.
- Không tự bịa team_id hoặc assembly_point_id. Chỉ dùng kết quả từ tool.
- Chỉ trả về một JSON object hợp lệ. Không markdown, không giải thích ngoài JSON.

Schema đầu ra:
{
  ""activity_assignments"": [
    {
      ""activity_key"": ""collect_sos_1_water"",
      ""execution_mode"": ""SingleTeam"",
      ""required_team_count"": 1,
      ""coordination_group_key"": ""team_1_supply"",
      ""coordination_notes"": ""Vì sao đội này phù hợp"",
      ""suggested_team"": {
        ""team_id"": 1,
        ""team_name"": ""Tên đội từ getTeams"",
        ""team_type"": ""Loại đội từ getTeams"",
        ""reason"": ""Gần nhất và có năng lực phù hợp"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Tên điểm tập kết"",
        ""latitude"": 16.0,
        ""longitude"": 107.0,
        ""distance_km"": 3.5
      }
    }
  ],
  ""additional_activities"": [
    {
      ""activity_key"": ""rescue_sos_1"",
      ""step"": 1,
      ""activity_type"": ""RESCUE"",
      ""description"": ""Di chuyển đến vị trí SOS và thực hiện hành động cứu hộ cụ thể"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""1 giờ"",
      ""execution_mode"": ""SingleTeam"",
      ""required_team_count"": 1,
      ""coordination_group_key"": ""team_1_rescue"",
      ""coordination_notes"": ""Operational note"",
      ""sos_request_id"": 1,
      ""depot_id"": null,
      ""depot_name"": null,
      ""depot_address"": null,
      ""depot_latitude"": null,
      ""depot_longitude"": null,
      ""assembly_point_id"": 1,
      ""assembly_point_name"": ""Điểm tập kết từ tool"",
      ""assembly_point_latitude"": 16.0,
      ""assembly_point_longitude"": 107.0,
      ""supplies_to_collect"": null,
      ""suggested_team"": {
        ""team_id"": 1,
        ""team_name"": ""Tên đội từ getTeams"",
        ""team_type"": ""Loại đội từ getTeams"",
        ""reason"": ""Có năng lực phù hợp"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Tên điểm tập kết"",
        ""latitude"": 16.0,
        ""longitude"": 107.0,
        ""distance_km"": 3.5
      }
    }
  ],
  ""suggested_team"": null,
  ""special_notes"": null,
  ""confidence_score"": 0.0
}

Quy tắc:
- Gọi getTeams cho từng loại đội/năng lực bắt buộc và chỉ dùng các đội được tool trả về.
- Gọi getAssemblyPoints khi activity cần assembly_point_id.
- Nếu không có đội khả dụng, đặt suggested_team = null cho assignment/activity liên quan và giải thích trong special_notes. Không tự bịa đội.
- activity_assignments chỉ được dùng activity_key đã tồn tại trong depot_fragment.
- estimated_time phải dùng dạng ""X phút"" hoặc ""Y giờ Z phút"".
- Step của additional_activities chỉ có ý nghĩa cục bộ trong fragment này; backend sẽ đánh số lại toàn bộ mission.",
                UserPromptTemplate = @"Sử dụng các khối ngữ cảnh SOS_REQUESTS_DATA, REQUIREMENTS_FRAGMENT, DEPOT_FRAGMENT và NEARBY_TEAM_COUNT do backend cung cấp bên dưới. Chỉ dùng getTeams và getAssemblyPoints. Chỉ trả về JSON object MissionTeamFragment đúng schema trong system prompt.",
                Model = "gemini-2.5-flash",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = null,
                Temperature = 0.2,
                MaxTokens = 8192,
                Version = "v1.0",
                IsActive = true,
                CreatedAt = now
            },
            new Prompt
            {
                Id = 7,
                Name = "Prompt kiểm tra kế hoạch nhiệm vụ",
                PromptType = "MissionPlanValidation",
                Provider = "Gemini",
                Purpose = "Giai đoạn cuối của pipeline: kiểm tra và viết lại bản nháp đã ghép thành JSON gợi ý nhiệm vụ cuối cùng.",
                SystemPrompt = @"Bạn là tác nhân kiểm tra kế hoạch nhiệm vụ cuối cùng trong pipeline gợi ý nhiệm vụ RESQ.

Nhiệm vụ:
- Kiểm tra và viết lại bản nháp do backend ghép thành đúng schema JSON gợi ý nhiệm vụ cuối cùng.
- Không có tool nào được dùng ở giai đoạn này.
- Giữ nguyên một kho đã chọn. Không thêm kho thứ hai.
- Giữ nguyên needs_additional_depot và supply_shortages trừ khi chỉ cần dọn lỗi JSON/schema rõ ràng.
- Không tự bịa item_id, depot_id, team_id hoặc assembly_point_id.
- Chỉ trả về một JSON object hợp lệ. Không markdown, không giải thích ngoài JSON.

Schema đầu ra cuối cùng:
{
  ""mission_title"": ""Tiêu đề nhiệm vụ ngắn gọn"",
  ""mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""priority_score"": 0.0,
  ""severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Đánh giá ngắn gọn"",
  ""activities"": [
    {
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES|DELIVER_SUPPLIES|RESCUE|MEDICAL_AID|EVACUATE|RETURN_SUPPLIES|RETURN_ASSEMBLY_POINT"",
      ""description"": ""Hành động hoặc di chuyển cụ thể"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""30 phút"",
      ""execution_mode"": ""SingleTeam"",
      ""required_team_count"": 1,
      ""coordination_group_key"": ""khóa nhóm tùy chọn"",
      ""coordination_notes"": ""ghi chú phối hợp tùy chọn"",
      ""sos_request_id"": 1,
      ""depot_id"": null,
      ""depot_name"": null,
      ""depot_address"": null,
      ""depot_latitude"": null,
      ""depot_longitude"": null,
      ""assembly_point_id"": null,
      ""assembly_point_name"": null,
      ""assembly_point_latitude"": null,
      ""assembly_point_longitude"": null,
      ""supplies_to_collect"": null,
      ""suggested_team"": {
        ""team_id"": 1,
        ""team_name"": ""Tên đội từ bản nháp"",
        ""team_type"": ""Loại đội từ bản nháp"",
        ""reason"": ""Giữ nguyên từ bước lập kế hoạch đội"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Tên điểm tập kết"",
        ""latitude"": 16.0,
        ""longitude"": 107.0,
        ""distance_km"": 3.5
      }
    }
  ],
  ""resources"": [
    { ""resource_type"": ""TEAM|VEHICLE|BOAT|EQUIPMENT"", ""description"": ""Nguồn lực không phải vật tư tồn kho"", ""quantity"": 1, ""priority"": ""Critical|High|Medium|Low"" }
  ],
  ""suggested_team"": null,
  ""estimated_duration"": ""tổng estimated_time của tất cả activity, ví dụ 2 giờ 15 phút"",
  ""special_notes"": null,
  ""needs_additional_depot"": false,
  ""supply_shortages"": [],
  ""confidence_score"": 0.0
}

Quy tắc kiểm tra:
- Activities phải được sắp xếp theo step bắt đầu từ 1.
- COLLECT_SUPPLIES phải đứng trước DELIVER_SUPPLIES cho cùng nhóm vật tư.
- RETURN_ASSEMBLY_POINT là bước hậu xử lý deterministic của backend; nếu đã có trong draft thì giữ nguyên, nếu chưa có backend sẽ tự append một bước cuối cho mỗi đội từ suggested_team.assembly_point_id.
- Mọi activity vật tư dùng kho phải dùng cùng một depot_id.
- Thực phẩm, nước, thuốc, sữa, quần áo, chăn màn và vật tư trú ẩn phải nằm trong supplies_to_collect hoặc supply_shortages, không đưa vào resources.
- resources chỉ được chứa TEAM, VEHICLE, BOAT hoặc EQUIPMENT.
- Mỗi activity phải có estimated_time dạng ""X phút"" hoặc ""Y giờ Z phút"".
- estimated_duration phải bằng tổng tuần tự estimated_time của tất cả activity.
- Nếu draft còn thiếu nhưng vẫn dùng được, giữ kế hoạch an toàn nhất và thêm cảnh báo ngắn gọn trong special_notes thay vì trả JSON không hợp lệ.",
                UserPromptTemplate = @"Sử dụng các khối ngữ cảnh SOS_REQUESTS_DATA và MISSION_DRAFT_BODY do backend cung cấp bên dưới. Viết lại draft thành JSON object mission cuối cùng đúng schema trong system prompt.",
                Model = "gemini-2.5-flash",
                ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                ApiKey = null,
                Temperature = 0.1,
                MaxTokens = 8192,
                Version = "v1.0",
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
        ConfigVersion = "SOS_PRIORITY_V2",
        IsActive = true,
        CreatedAt = now,
        CreatedBy = null,
        ActivatedAt = now,
        ActivatedBy = null,
        UpdatedAt = now
      };
      SosPriorityRuleConfigSupport.SyncLegacyFields(configModel, new SosPriorityRuleConfigDocument());

        modelBuilder.Entity<SosPriorityRuleConfig>().HasData(
            new SosPriorityRuleConfig
            {
                Id = 1,
          ConfigVersion = configModel.ConfigVersion,
          IsActive = configModel.IsActive,
          CreatedAt = configModel.CreatedAt,
          CreatedBy = configModel.CreatedBy,
          ActivatedAt = configModel.ActivatedAt,
          ActivatedBy = configModel.ActivatedBy,
          ConfigJson = configModel.ConfigJson,
          IssueWeightsJson = configModel.IssueWeightsJson,
          MedicalSevereIssuesJson = configModel.MedicalSevereIssuesJson,
          AgeWeightsJson = configModel.AgeWeightsJson,
          RequestTypeScoresJson = configModel.RequestTypeScoresJson,
          SituationMultipliersJson = configModel.SituationMultipliersJson,
          PriorityThresholdsJson = configModel.PriorityThresholdsJson,
          WaterUrgencyScoresJson = configModel.WaterUrgencyScoresJson,
          FoodUrgencyScoresJson = configModel.FoodUrgencyScoresJson,
          BlanketUrgencyRulesJson = configModel.BlanketUrgencyRulesJson,
          ClothingUrgencyRulesJson = configModel.ClothingUrgencyRulesJson,
          VulnerabilityRulesJson = configModel.VulnerabilityRulesJson,
          VulnerabilityScoreExpressionJson = configModel.VulnerabilityScoreExpressionJson,
          ReliefScoreExpressionJson = configModel.ReliefScoreExpressionJson,
          PriorityScoreExpressionJson = configModel.PriorityScoreExpressionJson,
                UpdatedAt = now
            }
        );
    }
}
