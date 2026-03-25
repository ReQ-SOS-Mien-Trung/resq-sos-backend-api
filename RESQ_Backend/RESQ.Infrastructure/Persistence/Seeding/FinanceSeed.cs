using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class FinanceSeed
{
    public static void SeedFinance(this ModelBuilder modelBuilder)
    {
        var fundSeedTime = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc);

        // 0. Payment Methods
        var paymentMethods = new List<PaymentMethod>
        {
            new PaymentMethod { Id = 1, Code = "PAYOS", Name = "Chuyển khoản Ngân hàng (QR Code)", IsActive = true },
            new PaymentMethod { Id = 2, Code = "MOMO", Name = "Ví điện tử MoMo", IsActive = true },
            new PaymentMethod { Id = 3, Code = "ZALOPAY", Name = "Ví điện tử ZaloPay", IsActive = true }
        };
        modelBuilder.Entity<PaymentMethod>().HasData(paymentMethods);

        // 0.1 Depot Funds (ví kho) — seed sẵn hạn mức tự ứng cho từng kho
        var depotFunds = new List<DepotFund>
        {
            new DepotFund { Id = 1, DepotId = 1, Balance = 120_000_000m, MaxAdvanceLimit = 80_000_000m, LastUpdatedAt = fundSeedTime },
            new DepotFund { Id = 2, DepotId = 2, Balance = 90_000_000m,  MaxAdvanceLimit = 60_000_000m, LastUpdatedAt = fundSeedTime },
            new DepotFund { Id = 3, DepotId = 3, Balance = 70_000_000m,  MaxAdvanceLimit = 40_000_000m, LastUpdatedAt = fundSeedTime },
            new DepotFund { Id = 4, DepotId = 4, Balance = 150_000_000m, MaxAdvanceLimit = 100_000_000m, LastUpdatedAt = fundSeedTime }
        };
        modelBuilder.Entity<DepotFund>().HasData(depotFunds);

        // 0.2 Depot Fund Transactions — dữ liệu mẫu lịch sử ví kho
        var depotFundTransactions = new List<DepotFundTransaction>
        {
            new DepotFundTransaction
            {
                Id = 1,
                DepotFundId = 1,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 200_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1001,
                Note = "Admin cấp quỹ đầu kỳ cho kho Huế",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 5, 2, 0, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 2,
                DepotFundId = 1,
                TransactionType = DepotFundTransactionType.Deduction.ToString(),
                Amount = 80_000_000m,
                ReferenceType = "VatInvoice",
                ReferenceId = 1,
                Note = "Nhập hàng quý I",
                CreatedBy = SeedConstants.ManagerUserId,
                CreatedAt = new DateTime(2026, 2, 2, 3, 30, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 3,
                DepotFundId = 2,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 120_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1002,
                Note = "Admin cấp quỹ kho Đà Nẵng",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 8, 2, 15, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 4,
                DepotFundId = 2,
                TransactionType = DepotFundTransactionType.SelfAdvance.ToString(),
                Amount = 30_000_000m,
                ReferenceType = "VatInvoice",
                ReferenceId = 2,
                Note = "Kho tự ứng khi nhập hàng vượt số dư",
                CreatedBy = SeedConstants.Manager2UserId,
                CreatedAt = new DateTime(2026, 2, 20, 4, 0, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 5,
                DepotFundId = 2,
                TransactionType = DepotFundTransactionType.DebtRepayment.ToString(),
                Amount = 20_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1003,
                Note = "Trả một phần nợ tự ứng sau khi được cấp bổ sung",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 3, 1, 1, 45, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 6,
                DepotFundId = 3,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 90_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1004,
                Note = "Admin cấp quỹ kho Hà Tĩnh",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 10, 2, 10, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 7,
                DepotFundId = 4,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 260_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1005,
                Note = "Admin cấp quỹ kho trung tâm",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 3, 1, 30, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 8,
                DepotFundId = 4,
                TransactionType = DepotFundTransactionType.Deduction.ToString(),
                Amount = 110_000_000m,
                ReferenceType = "VatInvoice",
                ReferenceId = 3,
                Note = "Nhập vật tư y tế và cứu hộ",
                CreatedBy = SeedConstants.Manager4UserId,
                CreatedAt = new DateTime(2026, 2, 14, 3, 20, 0, DateTimeKind.Utc)
            }
        };
        modelBuilder.Entity<DepotFundTransaction>().HasData(depotFundTransactions);

        // 1. Fund Campaigns
        var fundCampaigns = new List<FundCampaign>
        {
            new FundCampaign
            {
                Id = 1,
                Code = "FLOOD_RELIEF_2026",
                Name = "Quỹ Hỗ Trợ Nạn Nhân Lũ Lụt Miền Trung 2026",
                Region = "Miền Trung",
                CampaignStartDate = new DateOnly(2026, 1, 1),
                CampaignEndDate = new DateOnly(2026, 12, 31),
                TargetAmount = 1000000000, // 1 tỷ VND
                TotalAmount = 7500000,     // Tổng demo
                CurrentBalance = 7500000,  // Số dư hiện tại
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
                CampaignStartDate = new DateOnly(2025, 1, 15),
                CampaignEndDate = new DateOnly(2025, 3, 31),
                TargetAmount = 500000000, // 500 triệu VND
                TotalAmount = 520000000,   // Đã đạt mục tiêu
                CurrentBalance = 520000000, // Số dư hiện tại
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
                OrderId = "2607150001",
                TransactionId = "TRX-001",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Note = "Mong bà con sớm vượt qua khó khăn.",
                PaymentAuditInfo = "[Bank:MBBANK-1234567890]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2026, 1, 15, 10, 25, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 2,
                FundCampaignId = 1,
                DonorName = "Trần Thị B",
                DonorEmail = "tranthib@example.com",
                Amount = 2000000,
                OrderId = "2607160002",
                TransactionId = "TRX-002",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2026, 1, 16, 14, 15, 0, DateTimeKind.Utc),
                Note = "Ủng hộ miền Trung ruột thịt.",
                PaymentAuditInfo = "[Bank:VIETCOMBANK-0987654321]", 
                IsPrivate = true, 
                CreatedAt = new DateTime(2026, 1, 16, 14, 10, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 3,
                FundCampaignId = 1,
                DonorName = "Lê Văn C",
                DonorEmail = "levanc@example.com",
                Amount = 5000000,
                OrderId = "2608010003",
                TransactionId = "TRX-003",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 2, // MoMo
                PaidAt = new DateTime(2026, 2, 1, 09, 00, 0, DateTimeKind.Utc),
                Note = "Góp một phần nhỏ bé.",
                PaymentAuditInfo = "[MoMo:TransId=99887766,Type=captureWallet]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2026, 2, 1, 08, 55, 0, DateTimeKind.Utc)
            },

            // Donations for Campaign 2 (Closed)
            new Donation
            {
                Id = 4,
                FundCampaignId = 2,
                DonorName = "Công ty TNHH ABC",
                DonorEmail = "contact@abc.vn",
                Amount = 50000000,
                OrderId = "2502100004",
                TransactionId = "TRX-004",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 1, // PayOS
                PaidAt = new DateTime(2025, 2, 10, 11, 20, 0, DateTimeKind.Utc),
                Note = "Hỗ trợ thiết bị y tế cho bệnh viện.",
                PaymentAuditInfo = "[Bank:BIDV-555666777]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2025, 2, 10, 11, 15, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 5,
                FundCampaignId = 2,
                DonorName = "Phạm Văn D",
                DonorEmail = "phamvand@example.com",
                Amount = 200000,
                OrderId = "2502150005",
                TransactionId = "TRX-005",
                Status = Status.Succeed.ToString(),
                PaymentMethodId = 2, // MoMo
                PaidAt = new DateTime(2025, 2, 15, 16, 45, 0, DateTimeKind.Utc),
                Note = "Chúc các bác sĩ nhiều sức khỏe.",
                PaymentAuditInfo = "[MoMo:TransId=55443322,Type=qr]",
                IsPrivate = true,
                CreatedAt = new DateTime(2025, 2, 15, 16, 40, 0, DateTimeKind.Utc)
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
                CreatedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
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
                CreatedAt = new DateTime(2026, 1, 16, 14, 15, 0, DateTimeKind.Utc)
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
                CreatedAt = new DateTime(2026, 2, 1, 09, 00, 0, DateTimeKind.Utc)
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
                CreatedAt = new DateTime(2025, 2, 10, 11, 20, 0, DateTimeKind.Utc)
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
                CreatedAt = new DateTime(2025, 2, 15, 16, 45, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<FundTransaction>().HasData(transactions);
    }
}
