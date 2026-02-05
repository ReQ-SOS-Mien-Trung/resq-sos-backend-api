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
            }
        );
    }
}
