using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class FinanceSeed
{
    public static void SeedFinance(this ModelBuilder modelBuilder)
    {
        // Note: We are setting CreatedBy to null to avoid Foreign Key constraint violations
        // because we don't know the exact User IDs generated in SeedIdentity().
        // The FK constraint requires the User ID to actually exist in the users table.

        var fundCampaigns = new List<FundCampaign>
        {
            new FundCampaign
            {
                Id = 1,
                Code = "FLOOD_RELIEF_2026",
                Name = "Quỹ Hỗ Trợ Nạn Nhân Lũ Lụt Miền Trung 2026",
                Region = "Miền Trung",
                CampaignStartDate = new DateOnly(2024, 7, 1),
                CampaignEndDate = new DateOnly(2024, 9, 30),
                TargetAmount = 1000000000, // 1 billion VND
                TotalAmount = 750000000,   // 750 million VND collected
                Status = FundCampaignStatus.Active.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new FundCampaign
            {
                Id = 2,
                Code = "MEDICAL_SUPPLY_HN",
                Name = "Quỹ Cung Cấp Thiết Bị Y Tế Huế",
                Region = "Huế",
                CampaignStartDate = new DateOnly(2024, 1, 15),
                CampaignEndDate = new DateOnly(2024, 3, 31),
                TargetAmount = 500000000, // 500 million VND
                TotalAmount = 520000000,   // Exceeded target
                Status = FundCampaignStatus.Closed.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            }
        };

        modelBuilder.Entity<FundCampaign>().HasData(fundCampaigns);
    }
}
