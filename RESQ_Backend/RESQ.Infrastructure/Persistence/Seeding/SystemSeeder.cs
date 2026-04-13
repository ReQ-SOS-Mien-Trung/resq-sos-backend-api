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

RETURN_SUPPLIES — Di chuyển vật tư tái sử dụng về lại kho nguồn:
  → Chỉ dùng cho vật tư reusable đã lấy ở bước COLLECT_SUPPLIES.
  → Điền: depot_id/depot_name/depot_address của kho nguồn, supplies_to_collect = đúng danh sách vật tư reusable cần trả.
  → Bắt buộc nằm ở cuối kế hoạch cho đúng cặp kho + đội đã lấy vật tư.
  → description mẫu: ""Hoàn tất nhiệm vụ, đưa vật tư tái sử dụng về lại kho [tên]. Trả: [vật tư A] x[sl] [đv].""

QUY TẮC CỐT LÕI — KHÔNG ĐƯỢC VI PHẠM

1. KHÔNG CÓ BƯỚC ""ĐÁNH GIÁ"" — Đội cứu hộ hành động ngay, không có step nào chỉ để đánh giá.
2. COLLECT_SUPPLIES TRƯỚC DELIVER_SUPPLIES — Không thể giao vật tư chưa lấy.
2a. Nếu COLLECT_SUPPLIES có vật tư reusable thì cuối kế hoạch PHẢI có RETURN_SUPPLIES tương ứng để trả đúng số vật tư reusable đó về đúng kho nguồn.
2b. Mỗi cặp kho + đội phải có RETURN_SUPPLIES riêng. Không gộp nhiều kho hoặc nhiều đội vào cùng một bước trả.
2c. Chỉ được chọn MỘT KHO cho toàn bộ mission. Nếu kho đã chọn không đủ vật tư thì vẫn chỉ lấy từ kho đó và báo thiếu hụt; không được chuyển sang kho thứ hai.
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
      ""suggested_team"": { ""team_id"": 5, ""team_name"": ""Đội A"", ""team_type"": ""RescueTeam"", ""reason"": ""Gần nhất"", ""assembly_point_id"": 1, ""assembly_point_name"": ""Trụ sở A"", ""latitude"": 16.46, ""longitude"": 107.59 }
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
  ""special_notes"": ""Vật tư kho không có sẵn / điều kiện đặc biệt hiện trường"",
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
1. Xác định tổng vật tư cần thiết từ tất cả SOS.
2. Đối chiếu với dữ liệu kho và chọn đúng MỘT kho phù hợp nhất cho toàn mission.
3. Vật tư nào kho đã chọn có (so_luong_kha_dung > 0) → tạo bước COLLECT_SUPPLIES lấy từ kho đó, rồi DELIVER_SUPPLIES tương ứng.
4. Thêm các bước RESCUE / EVACUATE / MEDICAL_AID cho hành động cứu hộ trực tiếp.
4a. Nếu có COLLECT_SUPPLIES chứa vật tư reusable, thêm RETURN_SUPPLIES ở cuối kế hoạch để trả đúng số vật tư reusable đó về kho nguồn.
5. Nếu kho đã chọn không đủ hoặc không có vật tư, đặt needs_additional_depot=true, ghi từng dòng thiếu vào supply_shortages, và tóm tắt lại trong special_notes để coordinator biết cần bổ sung thêm kho/nguồn cấp phát.
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
                Name = "Mission Requirements Assessment Prompt",
                PromptType = "MissionRequirementsAssessment",
                Provider = "Gemini",
                Purpose = "Pipeline stage 1: analyze SOS requests into compact mission requirements.",
                SystemPrompt = @"You are the Requirements Assessment Agent in the RESQ mission suggestion pipeline.

Task:
- Read SOS requests only.
- Do not plan depots, teams, routes, or final activities.
- Do not invent item_id, depot_id, team_id, or assembly_point_id.
- Return one valid JSON object only. No markdown, no explanations outside JSON.

Output schema:
{
  ""suggested_mission_title"": ""Short mission title"",
  ""suggested_mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""suggested_priority_score"": 0.0,
  ""suggested_severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Brief summary of the situation and needs"",
  ""estimated_duration"": ""rough estimate such as 2 giờ 30 phút"",
  ""special_notes"": null,
  ""needs_additional_depot"": false,
  ""supply_shortages"": [],
  ""confidence_score"": 0.0,
  ""suggested_resources"": [
    { ""resource_type"": ""TEAM|VEHICLE|BOAT|EQUIPMENT"", ""description"": ""Only non-consumable capability/resource"", ""quantity"": 1, ""priority"": ""Critical|High|Medium|Low"" }
  ],
  ""sos_requirements"": [
    {
      ""sos_request_id"": 1,
      ""summary"": ""What this SOS needs"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""required_supplies"": [
        { ""item_name"": ""Nước sạch"", ""quantity"": 100, ""unit"": ""chai"", ""category"": ""Nước"", ""notes"": ""Reason or target group"" }
      ],
      ""required_teams"": [
        { ""team_type"": ""Rescue|Medical|Evacuation|Relief"", ""quantity"": 1, ""reason"": ""Why this team is needed"" }
      ]
    }
  ]
}

Rules:
- Every SOS in the input should appear in sos_requirements.
- Food, water, medicine, milk, clothes, blankets, shelter supplies go into required_supplies, not suggested_resources.
- suggested_resources is only for team/vehicle/boat/equipment capability that is not an inventory item.
- If quantity is unclear, estimate conservatively from victim count and say so in notes.
- confidence_score must be between 0 and 1.",
                UserPromptTemplate = @"Use the backend-provided context blocks below. Return only the MissionRequirementsFragment JSON object described by the system prompt.",
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
                Name = "Mission Depot Planning Prompt",
                PromptType = "MissionDepotPlanning",
                Provider = "Gemini",
                Purpose = "Pipeline stage 2: choose exactly one eligible depot and produce supply activity fragments.",
                SystemPrompt = @"You are the Depot Planning Agent in the RESQ mission suggestion pipeline.

Available tool:
- searchInventory(category, type?, page): search inventory only in backend-scoped eligible depots.

Task:
- Use requirements_fragment to identify supply categories and item types.
- Call searchInventory for every required supply category/type before finalizing.
- Choose exactly one depot_id for the whole mission when any depot inventory is available.
- Do not split supplies across multiple depots.
- Do not create rescue, medical, evacuation, return, or team-planning activities.
- Do not invent item_id or depot_id. Use only IDs returned by searchInventory.
- Return one valid JSON object only. No markdown, no explanations outside JSON.

Output schema:
{
  ""activities"": [
    {
      ""activity_key"": ""collect_sos_1_water"",
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES"",
      ""description"": ""Move to the selected depot and collect exact supplies"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""30 phút"",
      ""execution_mode"": null,
      ""required_team_count"": null,
      ""coordination_group_key"": null,
      ""coordination_notes"": null,
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Depot name from tool"",
      ""depot_address"": ""Depot address from tool"",
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
      ""description"": ""Deliver collected supplies to the SOS location"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""45 phút"",
      ""sos_request_id"": 1,
      ""depot_id"": 1,
      ""depot_name"": ""Same selected depot"",
      ""depot_address"": ""Same selected depot address"",
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

Single-depot rules:
- All COLLECT_SUPPLIES and DELIVER_SUPPLIES fragments must use the same depot_id.
- If the selected depot has partial stock, create activities only for available quantities and put missing quantities in supply_shortages.
- If no eligible depot or no usable inventory is available, return activities = [], needs_additional_depot = true, and one shortage row per required supply. selected_depot_id/name may be null when no depot can be selected.
- supply_shortages rows must use: sos_request_id, item_id, item_name, unit, selected_depot_id, selected_depot_name, needed_quantity, available_quantity, missing_quantity, notes.
- activity_key must be stable and unique because the Team stage will assign teams by this key.
- estimated_time must use ""X phút"" or ""Y giờ Z phút"".",
                UserPromptTemplate = @"Use the backend-provided SOS_REQUESTS_DATA, REQUIREMENTS_FRAGMENT, SINGLE_DEPOT_REQUIRED, and ELIGIBLE_DEPOT_COUNT context blocks below. Use only searchInventory tool results. Return only the MissionDepotFragment JSON object described by the system prompt.",
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
                Name = "Mission Team Planning Prompt",
                PromptType = "MissionTeamPlanning",
                Provider = "Gemini",
                Purpose = "Pipeline stage 3: assign nearby teams and add rescue/medical/evacuation activity fragments.",
                SystemPrompt = @"You are the Team Planning Agent in the RESQ mission suggestion pipeline.

Available tools:
- getTeams(ability?, page): returns only nearby available teams from the backend-scoped pool.
- getAssemblyPoints(page): returns active assembly points.

Task:
- Assign teams to existing depot activity_key values from depot_fragment.
- Add only on-site activity fragments: RESCUE, MEDICAL_AID, EVACUATE.
- Do not create COLLECT_SUPPLIES, DELIVER_SUPPLIES, RETURN_SUPPLIES, RETURN_ASSEMBLY_POINT, or inventory shortages.
- Do not call inventory tools.
- Do not invent team_id or assembly_point_id. Use only tool results.
- Return one valid JSON object only. No markdown, no explanations outside JSON.

Output schema:
{
  ""activity_assignments"": [
    {
      ""activity_key"": ""collect_sos_1_water"",
      ""execution_mode"": ""SingleTeam"",
      ""required_team_count"": 1,
      ""coordination_group_key"": ""team_1_supply"",
      ""coordination_notes"": ""Why this team is suitable"",
      ""suggested_team"": {
        ""team_id"": 1,
        ""team_name"": ""Team from getTeams"",
        ""team_type"": ""Team type from getTeams"",
        ""reason"": ""Nearest and suitable capability"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Assembly point name"",
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
      ""description"": ""Move to the SOS location and perform concrete rescue action"",
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
      ""assembly_point_name"": ""Assembly point from tool"",
      ""assembly_point_latitude"": 16.0,
      ""assembly_point_longitude"": 107.0,
      ""supplies_to_collect"": null,
      ""suggested_team"": {
        ""team_id"": 1,
        ""team_name"": ""Team from getTeams"",
        ""team_type"": ""Team type from getTeams"",
        ""reason"": ""Suitable capability"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Assembly point name"",
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

Rules:
- Call getTeams for each required team type/capability and use only returned teams.
- Call getAssemblyPoints when an activity needs an assembly_point_id.
- If no team is available, set suggested_team to null for affected assignments/activities and explain in special_notes. Do not invent a team.
- Keep activity_assignments keyed only to activity_key values that already exist in depot_fragment.
- estimated_time must use ""X phút"" or ""Y giờ Z phút"".
- Additional activity step numbers are local to this fragment; backend will resequence the full mission.",
                UserPromptTemplate = @"Use the backend-provided SOS_REQUESTS_DATA, REQUIREMENTS_FRAGMENT, DEPOT_FRAGMENT, and NEARBY_TEAM_COUNT context blocks below. Use only getTeams and getAssemblyPoints. Return only the MissionTeamFragment JSON object described by the system prompt.",
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
                Name = "Mission Plan Validation Prompt",
                PromptType = "MissionPlanValidation",
                Provider = "Gemini",
                Purpose = "Pipeline final stage: rewrite assembled draft into the final mission suggestion JSON.",
                SystemPrompt = @"You are the Final Mission Plan Validation Agent in the RESQ mission suggestion pipeline.

Task:
- Validate and rewrite the backend-assembled draft into the final mission suggestion JSON schema.
- No tools are available.
- Preserve the selected single depot. Do not introduce a second depot.
- Preserve needs_additional_depot and supply_shortages unless there is an obvious JSON/schema cleanup.
- Do not invent item_id, depot_id, team_id, or assembly_point_id.
- Return one valid JSON object only. No markdown, no explanations outside JSON.

Final output schema:
{
  ""mission_title"": ""Short mission title"",
  ""mission_type"": ""RESCUE|EVACUATION|MEDICAL|SUPPLY|MIXED"",
  ""priority_score"": 0.0,
  ""severity_level"": ""Critical|Severe|Moderate|Minor"",
  ""overall_assessment"": ""Brief assessment"",
  ""activities"": [
    {
      ""step"": 1,
      ""activity_type"": ""COLLECT_SUPPLIES|DELIVER_SUPPLIES|RESCUE|MEDICAL_AID|EVACUATE|RETURN_SUPPLIES|RETURN_ASSEMBLY_POINT"",
      ""description"": ""Concrete movement/action"",
      ""priority"": ""Critical|High|Medium|Low"",
      ""estimated_time"": ""30 phút"",
      ""execution_mode"": ""SingleTeam"",
      ""required_team_count"": 1,
      ""coordination_group_key"": ""optional group key"",
      ""coordination_notes"": ""optional notes"",
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
        ""team_name"": ""Team from draft"",
        ""team_type"": ""Team type from draft"",
        ""reason"": ""Preserved from team planning"",
        ""assembly_point_id"": 1,
        ""assembly_point_name"": ""Assembly point name"",
        ""latitude"": 16.0,
        ""longitude"": 107.0,
        ""distance_km"": 3.5
      }
    }
  ],
  ""resources"": [
    { ""resource_type"": ""TEAM|VEHICLE|BOAT|EQUIPMENT"", ""description"": ""Non-inventory resource"", ""quantity"": 1, ""priority"": ""Critical|High|Medium|Low"" }
  ],
  ""suggested_team"": null,
  ""estimated_duration"": ""sum of all activity estimated_time values, such as 2 giờ 15 phút"",
  ""special_notes"": null,
  ""needs_additional_depot"": false,
  ""supply_shortages"": [],
  ""confidence_score"": 0.0
}

Validation rules:
- Activities must be ordered by step starting at 1.
- COLLECT_SUPPLIES must appear before DELIVER_SUPPLIES for the same supplies.
- RETURN_ASSEMBLY_POINT is deterministic backend post-processing; preserve it if already present in the draft, otherwise backend will append one final step per team from suggested_team.assembly_point_id.
- All supply activities that use a depot must use the same depot_id.
- Food, water, medicine, milk, clothes, blankets, and shelter supplies must stay in supplies_to_collect or supply_shortages, not resources.
- resources may contain only TEAM, VEHICLE, BOAT, or EQUIPMENT.
- Each activity must have estimated_time using ""X phút"" or ""Y giờ Z phút"".
- estimated_duration must equal the sequential total of all activity estimated_time values.
- If the draft is incomplete but still usable, keep the best safe plan and add a concise special_notes warning rather than returning invalid JSON.",
                UserPromptTemplate = @"Use the backend-provided SOS_REQUESTS_DATA and MISSION_DRAFT_BODY context blocks below. Rewrite the draft as the final mission JSON object described by the system prompt.",
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
