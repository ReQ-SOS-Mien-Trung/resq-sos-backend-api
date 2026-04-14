using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class FinanceSeed
{
    public static void SeedFinance(this ModelBuilder modelBuilder)
    {
        var fundSeedTime = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc);
        var campaign1CreatedAt = new DateTime(2026, 2, 12, 9, 43, 42, 480, DateTimeKind.Utc).AddTicks(3636);
        var campaign2CreatedAt = new DateTime(2025, 8, 13, 9, 43, 42, 480, DateTimeKind.Utc).AddTicks(3648);

        // 0.1 Depot Funds (ví kho)
        // H?n m?c ?ng và du n? ?ng du?c qu?n lý ? c?p Depot.
        // FundSourceType = "Campaign", FundSourceId = 1 ? d?n t? chi?n d?ch FLOOD_RELIEF_2026
        var depotFunds = new List<DepotFund>
        {
            new DepotFund { Id = 1, DepotId = 1, Balance = 120_000_000m, LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 2, DepotId = 2, Balance = 130_000_000m, LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 3, DepotId = 3, Balance = 70_000_000m,  LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 4, DepotId = 4, Balance = 150_000_000m, LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 5, DepotId = 5, Balance = 18_000_000m,  LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 6, DepotId = 6, Balance = 22_000_000m,  LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 7, DepotId = 7, Balance = 8_000_000m,   LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 1 },
            new DepotFund { Id = 8, DepotId = 6, Balance = 1_500_000m,   LastUpdatedAt = fundSeedTime, FundSourceType = "Campaign", FundSourceId = 2 }
        };
        modelBuilder.Entity<DepotFund>().HasData(depotFunds);

        // 0.2 Depot Fund Transactions - d? li?u m?u l?ch s? ví kho
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
                Note = "Admin c?p qu? d?u k? cho kho Hu?",
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
                Note = "Nh?p hàng quý I",
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
                Note = "Admin c?p qu? kho Ðà N?ng",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 8, 2, 15, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 4,
                DepotFundId = 2,
                TransactionType = DepotFundTransactionType.PersonalAdvance.ToString(),
                Amount = 30_000_000m,
                ReferenceType = "VatInvoice",
                ReferenceId = 2,
                Note = "Kho t? ?ng khi nh?p hàng vu?t s? du",
                CreatedBy = SeedConstants.Manager2UserId,
                CreatedAt = new DateTime(2026, 2, 20, 4, 0, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 5,
                DepotFundId = 2,
                TransactionType = DepotFundTransactionType.AdvanceRepayment.ToString(),
                Amount = 20_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1003,
                Note = "Tr? m?t ph?n n? t? ?ng sau khi du?c c?p b? sung",
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
                Note = "Admin c?p qu? kho Hà Tinh",
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
                Note = "Admin c?p qu? kho trung tâm",
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
                Note = "Nh?p v?t ph?m y t? và c?u h?",
                CreatedBy = SeedConstants.Manager4UserId,
                CreatedAt = new DateTime(2026, 2, 14, 3, 20, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 9,
                DepotFundId = 5,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 18_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1006,
                Note = "Admin c?p qu? kho Thang Bình d? test dóng kho x? lý bên ngoài",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 12, 2, 0, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 10,
                DepotFundId = 6,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 22_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1007,
                Note = "Admin c?p qu? kho Qu?ng Ninh d? test dóng kho chuy?n kho",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 14, 2, 15, 0, DateTimeKind.Utc)
            },
            new DepotFundTransaction
            {
                Id = 11,
                DepotFundId = 7,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = 8_000_000m,
                ReferenceType = "FundingRequest",
                ReferenceId = 1008,
                Note = "Admin c?p qu? kho Ngh? An d? test dóng kho tr?ng",
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = new DateTime(2026, 1, 16, 1, 45, 0, DateTimeKind.Utc)
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
                Name = "Qu? H? Tr? N?n Nhân Lu L?t Mi?n Trung 2026",
                Region = "Mi?n Trung",
                CampaignStartDate = new DateOnly(2026, 1, 1),
                CampaignEndDate = new DateOnly(2026, 12, 31),
                TargetAmount = 1000000000, // 1 t? VND
                TotalAmount = 7500000,     // T?ng demo
                CurrentBalance = 7500000,  // S? du hi?n t?i
                Status = FundCampaignStatus.Active.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = campaign1CreatedAt
            },
            new FundCampaign
            {
                Id = 2,
                Code = "MEDICAL_SUPPLY_HN",
                Name = "Qu? Cung C?p Thi?t B? Y T? Hu?",
                Region = "Hu?",
                CampaignStartDate = new DateOnly(2025, 1, 15),
                CampaignEndDate = new DateOnly(2025, 3, 31),
                TargetAmount = 500000000, // 500 tri?u VND
                TotalAmount = 520000000,   // Ðã d?t m?c tiêu
                CurrentBalance = 520000000, // S? du hi?n t?i
                Status = FundCampaignStatus.Closed.ToString(),
                CreatedBy = SeedConstants.AdminUserId,
                CreatedAt = campaign2CreatedAt
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
                DonorName = "Nguy?n Van A",
                DonorEmail = "nguyenvana@example.com",
                Amount = 500000,
                OrderId = "2607150001",
                TransactionId = "TRX-001",
                Status = Status.Succeed.ToString(),
                PaymentMethodCode = PaymentMethodCode.PAYOS,
                PaidAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Note = "Mong bà con s?m vu?t qua khó khan.",
                PaymentAuditInfo = "[Bank:MBBANK-1234567890]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2026, 1, 15, 10, 25, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 2,
                FundCampaignId = 1,
                DonorName = "Tr?n Th? B",
                DonorEmail = "tranthib@example.com",
                Amount = 2000000,
                OrderId = "2607160002",
                TransactionId = "TRX-002",
                Status = Status.Succeed.ToString(),
                PaymentMethodCode = PaymentMethodCode.PAYOS,
                PaidAt = new DateTime(2026, 1, 16, 14, 15, 0, DateTimeKind.Utc),
                Note = "?ng h? mi?n Trung ru?t th?t.",
                PaymentAuditInfo = "[Bank:VIETCOMBANK-0987654321]", 
                IsPrivate = true, 
                CreatedAt = new DateTime(2026, 1, 16, 14, 10, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 3,
                FundCampaignId = 1,
                DonorName = "Lê Van C",
                DonorEmail = "levanc@example.com",
                Amount = 5000000,
                OrderId = "2608010003",
                TransactionId = "TRX-003",
                Status = Status.Succeed.ToString(),
                PaymentMethodCode = PaymentMethodCode.MOMO,
                PaidAt = new DateTime(2026, 2, 1, 09, 00, 0, DateTimeKind.Utc),
                Note = "Góp m?t ph?n nh? bé.",
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
                PaymentMethodCode = PaymentMethodCode.PAYOS,
                PaidAt = new DateTime(2025, 2, 10, 11, 20, 0, DateTimeKind.Utc),
                Note = "H? tr? thi?t b? y t? cho b?nh vi?n.",
                PaymentAuditInfo = "[Bank:BIDV-555666777]", 
                IsPrivate = false,
                CreatedAt = new DateTime(2025, 2, 10, 11, 15, 0, DateTimeKind.Utc)
            },
            new Donation
            {
                Id = 5,
                FundCampaignId = 2,
                DonorName = "Ph?m Van D",
                DonorEmail = "phamvand@example.com",
                Amount = 200000,
                OrderId = "2502150005",
                TransactionId = "TRX-005",
                Status = Status.Succeed.ToString(),
                PaymentMethodCode = PaymentMethodCode.MOMO,
                PaidAt = new DateTime(2025, 2, 15, 16, 45, 0, DateTimeKind.Utc),
                Note = "Chúc các bác si nhi?u s?c kh?e.",
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
