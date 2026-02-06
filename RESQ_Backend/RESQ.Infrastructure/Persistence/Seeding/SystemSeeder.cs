using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class SystemSeeder
{
    public static void SeedSystem(this ModelBuilder modelBuilder)
    {
        SeedNotifications(modelBuilder);
        SeedPrompts(modelBuilder);
    }

    private static void SeedNotifications(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Notification>().HasData(
            new Notification
            {
                Id = 1,
                UserId = SeedConstants.AdminUserId,
                Content = "Có yêu cầu cứu hộ mới cần xử lý",
                CreatedAt = now
            },
            new Notification
            {
                Id = 2,
                UserId = SeedConstants.CoordinatorUserId,
                Content = "Nhiệm vụ #1 đã được giao cho đội của bạn",
                CreatedAt = now
            }
        );
    }

    private static void SeedPrompts(ModelBuilder modelBuilder)
    {
        // Adjusted to Utc
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Prompt>().HasData(
            new Prompt
            {
                Id = 1,
                Name = "SOS Analysis Prompt",
                Purpose = "Phân tích tin nhắn SOS để trích xuất thông tin",
                SystemPrompt = "Bạn là một AI chuyên phân tích các tin nhắn cầu cứu trong thiên tai...",
                Temperature = 0.3,
                MaxTokens = 1000,
                Version = "v1.0",
                CreatedAt = now
            },
            new Prompt
            {
                Id = 2,
                Name = "Mission Planning Prompt",
                Purpose = "Lập kế hoạch nhiệm vụ cứu trợ",
                SystemPrompt = "Bạn là một AI hỗ trợ lập kế hoạch nhiệm vụ cứu trợ thiên tai...",
                Temperature = 0.5,
                MaxTokens = 2000,
                Version = "v1.0",
                CreatedAt = now
            },
            new Prompt
            {
                Id = 3,
                Name = "SOS_PRIORITY_ANALYSIS",
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
                Temperature = 0.3,
                MaxTokens = 1024,
                Version = "1.0",
                CreatedAt = now
            }
        );
    }
}
