using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class FinanceSeed
{
    public static void SeedFinance(this ModelBuilder modelBuilder)
    {
        // 0. Payment Methods (Added)
        var paymentMethods = new List<PaymentMethod>
        {
            new PaymentMethod { Id = 1, Code = "PAYOS", Name = "Chuyá»ƒn khoáº£n NgÃ¢n hÃ ng (QR Code)", IsActive = true },
            new PaymentMethod { Id = 2, Code = "MOMO", Name = "VÃ­ Ä‘iá»‡n tá»­ MoMo", IsActive = true }
        };
        modelBuilder.Entity<PaymentMethod>().HasData(paymentMethods);

        // 1. Fund Campaigns
        var fundCampaigns = new List<FundCampaign>
        {
            new FundCampaign
            {
                Id = 1,
                Code = "FLOOD_RELIEF_2026",
                Name = "Quá»¹ Há»— Trá»£ Náº¡n NhÃ¢n LÅ© Lá»¥t Miá»n Trung 2026",
                Region = "Miá»n Trung",
                CampaignStartDate = new DateOnly(2024, 7, 1),
                CampaignEndDate = new DateOnly(2024, 9, 30),
                TargetAmount = 1000000000, // 1 tá»· VND
                TotalAmount = 7500000,     // Tá»•ng demo
                Status = FundCampaignStatus.Active.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new FundCampaign
            {
                Id = 2,
                Code = "MEDICAL_SUPPLY_HN",
                Name = "Quá»¹ Cung Cáº¥p Thiáº¿t Bá»‹ Y Táº¿ Huáº¿",
                Region = "Huáº¿",
                CampaignStartDate = new DateOnly(2024, 1, 15),
                CampaignEndDate = new DateOnly(2024, 3, 31),
                TargetAmount = 500000000, // 500 triá»‡u VND
                TotalAmount = 520000000,   // ÄÃ£ Ä‘áº¡t má»¥c tiÃªu
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
                DonorName = "Nguyá»…n VÄƒn A",
                DonorEmail = "nguyenvana@example.com",
                Amount = 500000,
                OrderId = "2407150001",
                TransactionId = "TRX-001",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2024, 7, 15, 10, 30, 0, DateTimeKind.Utc),
                Note = "Mong bÃ  con sá»›m vÆ°á»£t qua khÃ³ khÄƒn.",
                PaymentAuditInfo = "[Bank:MBBANK-1234567890]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 7, 15, 10, 25, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 2,
                FundCampaignId = 1,
                DonorName = "Tráº§n Thá»‹ B",
                DonorEmail = "tranthib@example.com",
                Amount = 2000000,
                OrderId = "2407160002",
                TransactionId = "TRX-002",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2024, 7, 16, 14, 15, 0, DateTimeKind.Utc),
                Note = "á»¦ng há»™ miá»n Trung ruá»™t thá»‹t.",
                PaymentAuditInfo = "[Bank:VIETCOMBANK-0987654321]", 
                IsPrivate = true, 
                CreatedAt = new DateTime(2024, 7, 16, 14, 10, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 3,
                FundCampaignId = 1,
                DonorName = "LÃª VÄƒn C",
                DonorEmail = "levanc@example.com",
                Amount = 5000000,
                OrderId = "2408010003",
                TransactionId = "TRX-003",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 2, // MoMo
                PaidAt = new DateTime(2024, 8, 1, 09, 00, 0, DateTimeKind.Utc),
                Note = "GÃ³p má»™t pháº§n nhá» bÃ©.",
                PaymentAuditInfo = "[MoMo:TransId=99887766,Type=captureWallet]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 8, 1, 08, 55, 0, DateTimeKind.Utc)
            },

            // Donations for Campaign 2 (Closed)
            new Donation
            {
                Id = 4,
                FundCampaignId = 2,
                DonorName = "CÃ´ng ty TNHH ABC",
                DonorEmail = "contact@abc.vn",
                Amount = 50000000,
                OrderId = "2402100004",
                TransactionId = "TRX-004",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2024, 2, 10, 11, 20, 0, DateTimeKind.Utc),
                Note = "Há»— trá»£ thiáº¿t bá»‹ y táº¿ cho bá»‡nh viá»‡n.",
                PaymentAuditInfo = "[Bank:BIDV-555666777]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2024, 2, 10, 11, 15, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 5,
                FundCampaignId = 2,
                DonorName = "Pháº¡m VÄƒn D",
                DonorEmail = "phamvand@example.com",
                Amount = 200000,
                OrderId = "2402150005",
                TransactionId = "TRX-005",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 2, // MoMo
                PaidAt = new DateTime(2024, 2, 15, 16, 45, 0, DateTimeKind.Utc),
                Note = "ChÃºc cÃ¡c bÃ¡c sÄ© nhiá»u sá»©c khá»e.",
                PaymentAuditInfo = "[MoMo:TransId=55443322,Type=qr]",
                IsPrivate = true,
                CreatedAt = new DateTime(2024, 2, 15, 16, 40, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<Donation>().HasData(donations);

        // 3. Fund Transactions
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
                ReferenceId = 1, 
                CreatedBy = null,
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
                ReferenceId = 2, 
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
                ReferenceId = 3, 
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
                ReferenceId = 4, 
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
                ReferenceId = 5, 
                CreatedBy = null,
                CreatedAt = new DateTime(2024, 2, 15, 16, 45, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<FundTransaction>().HasData(transactions);
    }
}

