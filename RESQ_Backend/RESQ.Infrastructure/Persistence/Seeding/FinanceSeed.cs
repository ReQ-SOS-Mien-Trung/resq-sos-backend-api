using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class FinanceSeed
{
    public static void SeedFinance(this ModelBuilder modelBuilder)
    {
        // 1. Fund Campaigns
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
                TargetAmount = 1000000000, // 1 tỷ VND
                TotalAmount = 7500000,     // Tổng demo
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
                TargetAmount = 500000000, // 500 triệu VND
                TotalAmount = 520000000,   // Đã đạt mục tiêu
                Status = FundCampaignStatus.Closed.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            }
        };

        modelBuilder.Entity<FundCampaign>().HasData(fundCampaigns);

        // 2. Donations
        var donations = new List<Donation>
        {
            // Donations for Campaign 1
            new Donation
            {
                Id = 1,
                FundCampaignId = 1,
                DonorName = "Nguyễn Văn A",
                DonorEmail = "nguyenvana@example.com",
                Amount = 500000,
                PayosOrderId = "2407150001",
                PayosTransactionId = "TRX-001",
                PayosStatus = PayOSStatus.Succeed.ToString(),
                PaidAt = new DateTime(2024, 7, 15, 10, 30, 0, DateTimeKind.Utc),
                Note = "Mong bà con sớm vượt qua khó khăn.",
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 7, 15, 10, 25, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 2,
                FundCampaignId = 1,
                DonorName = "Trần Thị B",
                DonorEmail = "tranthib@example.com",
                Amount = 2000000,
                PayosOrderId = "2407160002",
                PayosTransactionId = "TRX-002",
                PayosStatus = PayOSStatus.Succeed.ToString(),
                PaidAt = new DateTime(2024, 7, 16, 14, 15, 0, DateTimeKind.Utc),
                Note = "Ủng hộ miền Trung ruột thịt.",
                IsPrivate = true, // Ẩn danh
                CreatedAt = new DateTime(2024, 7, 16, 14, 10, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 3,
                FundCampaignId = 1,
                DonorName = "Lê Văn C",
                DonorEmail = "levanc@example.com",
                Amount = 5000000,
                PayosOrderId = "2408010003",
                PayosTransactionId = "TRX-003",
                PayosStatus = PayOSStatus.Succeed.ToString(),
                PaidAt = new DateTime(2024, 8, 1, 09, 00, 0, DateTimeKind.Utc),
                Note = "Góp một phần nhỏ bé.",
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 8, 1, 08, 55, 0, DateTimeKind.Utc)
            },

            // Donations for Campaign 2 (Closed)
            new Donation
            {
                Id = 4,
                FundCampaignId = 2,
                DonorName = "Công ty TNHH ABC",
                DonorEmail = "contact@abc.vn",
                Amount = 50000000,
                PayosOrderId = "2402100004",
                PayosTransactionId = "TRX-004",
                PayosStatus = PayOSStatus.Succeed.ToString(),
                PaidAt = new DateTime(2024, 2, 10, 11, 20, 0, DateTimeKind.Utc),
                Note = "Hỗ trợ thiết bị y tế cho bệnh viện.",
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 2, 10, 11, 15, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 5,
                FundCampaignId = 2,
                DonorName = "Phạm Văn D",
                DonorEmail = "phamvand@example.com",
                Amount = 200000,
                PayosOrderId = "2402150005",
                PayosTransactionId = "TRX-005",
                PayosStatus = PayOSStatus.Succeed.ToString(),
                PaidAt = new DateTime(2024, 2, 15, 16, 45, 0, DateTimeKind.Utc),
                Note = "Chúc các bác sĩ nhiều sức khỏe.",
                IsPrivate = true,
                CreatedAt = new DateTime(2024, 2, 15, 16, 40, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<Donation>().HasData(donations);

        // 3. Fund Transactions (Corresponding to successful donations)
        var transactions = new List<FundTransaction>
        {
            new FundTransaction
            {
                Id = 1,
                FundCampaignId = 1,
                Type = TransactionType.Donation.ToString(),
                Direction = "in",
                Amount = 500000,
                ReferenceType = TransactionReferenceType.Donation.ToString(),
                ReferenceId = 1, // Link to Donation Id 1
                CreatedBy = null, // System/Public
                CreatedAt = new DateTime(2024, 7, 15, 10, 30, 0, DateTimeKind.Utc)
            },
            new FundTransaction
            {
                Id = 2,
                FundCampaignId = 1,
                Type = TransactionType.Donation.ToString(),
                Direction = "in",
                Amount = 2000000,
                ReferenceType = TransactionReferenceType.Donation.ToString(),
                ReferenceId = 2, // Link to Donation Id 2
                CreatedBy = null,
                CreatedAt = new DateTime(2024, 7, 16, 14, 15, 0, DateTimeKind.Utc)
            },
            new FundTransaction
            {
                Id = 3,
                FundCampaignId = 1,
                Type = TransactionType.Donation.ToString(),
                Direction = "in",
                Amount = 5000000,
                ReferenceType = TransactionReferenceType.Donation.ToString(),
                ReferenceId = 3, // Link to Donation Id 3
                CreatedBy = null,
                CreatedAt = new DateTime(2024, 8, 1, 09, 00, 0, DateTimeKind.Utc)
            },
            new FundTransaction
            {
                Id = 4,
                FundCampaignId = 2,
                Type = TransactionType.Donation.ToString(),
                Direction = "in",
                Amount = 50000000,
                ReferenceType = TransactionReferenceType.Donation.ToString(),
                ReferenceId = 4, // Link to Donation Id 4
                CreatedBy = null,
                CreatedAt = new DateTime(2024, 2, 10, 11, 20, 0, DateTimeKind.Utc)
            },
            new FundTransaction
            {
                Id = 5,
                FundCampaignId = 2,
                Type = TransactionType.Donation.ToString(),
                Direction = "in",
                Amount = 200000,
                ReferenceType = TransactionReferenceType.Donation.ToString(),
                ReferenceId = 5, // Link to Donation Id 5
                CreatedBy = null,
                CreatedAt = new DateTime(2024, 2, 15, 16, 45, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<FundTransaction>().HasData(transactions);
    }
}
