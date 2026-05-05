using Microsoft.EntityFrameworkCore;
using RESQ.Domain.Enum.Finance;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private async Task SeedFinanceAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var systemFund = new SystemFund
        {
            Name = "Quỹ điều phối hệ thống",
            Balance = 4_500_000_000m,
            LastUpdatedAt = seed.AnchorUtc
        };
        _db.SystemFunds.Add(systemFund);

        var campaignPlans = new List<(FundCampaign Campaign, decimal PlannedRaised)>();
        var donationRatios = new[] { 22m, 18m, 15m, 13m, 11m, 9m, 7m, 5m };

        for (var i = 0; i < 11; i++)
        {
            var isActiveCampaign = IsSeedActiveCampaign(i);
            var start = isActiveCampaign
                ? ActiveCampaignStartDate(i, seed.Options.AnchorDate)
                : DateOnly.FromDateTime(seed.StartUtc.AddDays(120 + i * 75));
            var end = isActiveCampaign
                ? ActiveCampaignEndDate(i, seed.Options.AnchorDate)
                : start.AddDays(45);
            var campaign = new FundCampaign
            {
                Code = $"FC-{start.Year}-B{i + 1:00}",
                Name = CampaignName(i),
                Region = "Huế - Đà Nẵng - Quảng Trị - Quảng Nam - Quảng Ngãi",
                CampaignStartDate = start,
                CampaignEndDate = end,
                TargetAmount = 1_500_000_000m + i * 150_000_000m,
                // Calculated from seeded donation/disbursement history below.
                TotalAmount = 0m,
                CurrentBalance = 0m,
                Status = isActiveCampaign
                    ? FundCampaignStatus.Active.ToString()
                    : FundCampaignStatus.Closed.ToString(),
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(start.ToDateTime(TimeOnly.MinValue)),
                LastModifiedBy = seed.Admins[0].Id,
                LastModifiedAt = seed.AnchorUtc.AddDays(-i),
                IsDeleted = false
            };

            seed.FundCampaigns.Add(campaign);
            campaignPlans.Add((campaign, 450_000_000m + i * 155_000_000m));
        }
        _db.FundCampaigns.AddRange(seed.FundCampaigns);
        await _db.SaveChangesAsync(cancellationToken);

        var donations = new List<Donation>();
        for (var campaignIndex = 0; campaignIndex < campaignPlans.Count; campaignIndex++)
        {
            var (campaign, plannedRaised) = campaignPlans[campaignIndex];
            var remaining = plannedRaised;
            var campaignStartLocal = campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(8, 0));

            for (var donationIndex = 0; donationIndex < donationRatios.Length; donationIndex++)
            {
                var amount = donationIndex == donationRatios.Length - 1
                    ? remaining
                    : decimal.Round(plannedRaised * donationRatios[donationIndex] / 100m, 0, MidpointRounding.AwayFromZero);
                remaining -= amount;

                var donorSeed = campaignIndex * 17 + donationIndex;
                var (last, first) = VietnameseName(donorSeed);
                var donorName = donationIndex % 3 == 0
                    ? OrganizationName(donorSeed)
                    : $"{last} {first}";

                var orderId = $"{campaign.CampaignStartDate:yyMMdd}{campaign.Id:00}{donationIndex + 1:0000}";
                var paidAtLocal = campaignStartLocal
                    .AddDays(Math.Min(40, donationIndex * 5 + campaignIndex % 3))
                    .AddHours(donationIndex % 5);
                var paidAtUtc = VnToUtc(paidAtLocal);

                donations.Add(new Donation
                {
                    FundCampaignId = campaign.Id,
                    DonorName = donorName,
                    DonorEmail = $"donor-c{campaign.Id:00}-{donationIndex + 1:000}@resq.vn",
                    Amount = amount,
                    OrderId = orderId,
                    TransactionId = $"DEMO-TRX-{campaign.Id:00}-{donationIndex + 1:0000}",
                    Status = Status.Succeed.ToString(),
                    PaymentMethodCode = donationIndex % 2 == 0 ? PaymentMethodCode.PAYOS : PaymentMethodCode.MOMO,
                    PaidAt = paidAtUtc,
                    Note = "Đóng góp ủng hộ chiến dịch miền Trung.",
                    PaymentAuditInfo = donationIndex % 2 == 0
                        ? $"[PAYOS:order={orderId}]"
                        : $"[MOMO:campaign={campaign.Id},seq={donationIndex + 1}]",
                    IsPrivate = donationIndex % 4 == 1,
                    CreatedAt = paidAtUtc.AddMinutes(-10)
                });
            }
        }

        _db.Donations.AddRange(donations);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(donations.Select(donation => new FundTransaction
        {
            FundCampaignId = donation.FundCampaignId,
            Type = TransactionType.Donation.ToString(),
            Direction = "in",
            Amount = donation.Amount,
            ReferenceType = TransactionReferenceType.Donation.ToString(),
            ReferenceId = donation.Id,
            CreatedBy = null,
            CreatedAt = donation.PaidAt ?? donation.CreatedAt
        }));

        var seededDisbursements = new List<CampaignDisbursement>();

        for (var i = 0; i < 42; i++)
        {
            var depot = seed.Depots[i % seed.Depots.Count];
            var approved = i < 30;
            var rejected = i >= 36;
            var created = RandomEventUtc(seed, i + 500);
            seed.FundingRequests.Add(new FundingRequest
            {
                DepotId = depot.Id,
                RequestedBy = seed.Managers[i % seed.Managers.Count].Id,
                TotalAmount = 12_000_000 + (i % 10) * 4_500_000,
                Description = "Bổ sung thuốc, áo mưa, nước uống và vật tư vệ sinh cho đợt mưa lũ",
                AttachmentUrl = $"https://cdn.resq.vn/funding/fr-{i + 1:000}.xlsx",
                Status = approved ? "Approved" : rejected ? "Rejected" : "Pending",
                ApprovedCampaignId = approved ? seed.FundCampaigns[i % seed.FundCampaigns.Count].Id : null,
                ReviewedBy = approved || rejected ? seed.Admins[0].Id : null,
                ReviewedAt = approved || rejected ? created.AddHours(6) : null,
                RejectionReason = rejected ? "Chưa đủ báo giá kèm theo" : null,
                CreatedAt = created
            });
        }
        _db.FundingRequests.AddRange(seed.FundingRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.FundingRequests)
        {
            for (var j = 0; j < 4; j++)
            {
                var item = seed.ItemModels[(request.Id * 5 + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 350_000 + j * 120_000 : 18_000 + j * 7_000;
                var quantity = item.ItemType == "Reusable" ? 3 + j : 50 + j * 20;
                _db.FundingRequestItems.Add(new FundingRequestItem
                {
                    FundingRequestId = request.Id,
                    Row = j + 1,
                    ItemName = item.Name ?? "Vật phẩm cứu trợ",
                    CategoryCode = seed.Categories.First(c => c.Id == item.CategoryId).Code ?? "GENERAL",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    ItemType = item.ItemType ?? "Consumable",
                    TargetGroup = "Adult",
                    VolumePerUnit = item.VolumePerUnit ?? 0,
                    WeightPerUnit = item.WeightPerUnit ?? 0,
                    ReceivedDate = DateOnly.FromDateTime(request.CreatedAt),
                    ExpiredDate = item.ItemType == "Consumable" ? DateOnly.FromDateTime(request.CreatedAt.AddMonths(8)) : null,
                    Notes = "Dòng seed demo cho funding request"
                });
            }
        }

        foreach (var request in seed.FundingRequests.Where(r => r.Status == "Approved"))
        {
            var disbursement = new CampaignDisbursement
            {
                FundCampaignId = request.ApprovedCampaignId!.Value,
                DepotId = request.DepotId,
                Amount = request.TotalAmount,
                Purpose = $"Duyệt yêu cầu cấp quỹ #{request.Id}",
                Type = "FundingRequestApproval",
                FundingRequestId = request.Id,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = request.ReviewedAt ?? request.CreatedAt.AddHours(8)
            };
            _db.CampaignDisbursements.Add(disbursement);
            await _db.SaveChangesAsync(cancellationToken);
            seededDisbursements.Add(disbursement);

            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(request.Id + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 420_000 : 25_000;
                var quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40;
                _db.DisbursementItems.Add(new DisbursementItem
                {
                    CampaignDisbursementId = disbursement.Id,
                    ItemName = item.Name ?? "Vật phẩm",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    Note = "Mua theo kế hoạch giải ngân",
                    CreatedAt = disbursement.CreatedAt
                });
            }
        }

        var adminDisbursements = seed.FundCampaigns
            .Select((campaign, index) => new CampaignDisbursement
            {
                FundCampaignId = campaign.Id,
                DepotId = seed.Depots[(index * 2) % seed.Depots.Count].Id,
                Amount = 18_000_000m + (index % 5) * 4_000_000m,
                Purpose = "Admin chủ động cấp tiền cho kho theo kế hoạch dự phòng",
                Type = DisbursementType.AdminAllocation.ToString(),
                FundingRequestId = null,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(10, 0)).AddDays(28 + index % 5))
            })
            .ToList();

        _db.CampaignDisbursements.AddRange(adminDisbursements);
        seededDisbursements.AddRange(adminDisbursements);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(seededDisbursements.Select(disbursement => new FundTransaction
        {
            FundCampaignId = disbursement.FundCampaignId,
            Type = TransactionType.Allocation.ToString(),
            Direction = "out",
            Amount = disbursement.Amount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement.ToString(),
            ReferenceId = disbursement.Id,
            CreatedBy = disbursement.CreatedBy,
            CreatedAt = disbursement.CreatedAt
        }));

        var raisedByCampaign = donations
            .Where(d => d.FundCampaignId.HasValue && d.Amount.HasValue && d.Status == Status.Succeed.ToString())
            .GroupBy(d => d.FundCampaignId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount ?? 0m));

        var disbursedByCampaign = seededDisbursements
            .GroupBy(d => d.FundCampaignId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        foreach (var campaign in seed.FundCampaigns)
        {
            var totalRaised = raisedByCampaign.TryGetValue(campaign.Id, out var raised) ? raised : 0m;
            var totalDisbursed = disbursedByCampaign.TryGetValue(campaign.Id, out var disbursed) ? disbursed : 0m;

            campaign.TotalAmount = totalRaised;
            campaign.CurrentBalance = totalRaised - totalDisbursed;
        }

        var campaignDepotFunds = seededDisbursements
            .GroupBy(disbursement => new { disbursement.DepotId, disbursement.FundCampaignId })
            .Select(group => new DepotFund
            {
                DepotId = group.Key.DepotId,
                Balance = 0m,
                LastUpdatedAt = group.Max(disbursement => disbursement.CreatedAt),
                FundSourceType = FundSourceType.Campaign.ToString(),
                FundSourceId = group.Key.FundCampaignId
            })
            .OrderBy(fund => fund.DepotId)
            .ThenBy(fund => fund.FundSourceId)
            .ToList();

        var systemDepotFunds = seed.Depots
            .OrderBy(depot => depot.Id)
            .Select(depot => new DepotFund
            {
                DepotId = depot.Id,
                Balance = 0m,
                LastUpdatedAt = seed.AnchorUtc,
                FundSourceType = FundSourceType.SystemFund.ToString(),
                FundSourceId = systemFund.Id
            })
            .ToList();

        _db.DepotFunds.AddRange(campaignDepotFunds);
        _db.DepotFunds.AddRange(systemDepotFunds);
        await _db.SaveChangesAsync(cancellationToken);
        var depotFunds = await _db.DepotFunds.OrderBy(f => f.Id).ToListAsync(cancellationToken);

        var vatInvoices = await _db.VatInvoices.OrderBy(v => v.Id).ToListAsync(cancellationToken);

        // Calculate LiquidationRevenue needed to support all allocations from SystemFund
        decimal totalSystemFundNeeded = 0m;
        foreach (var fund in depotFunds)
        {
            if (fund.FundSourceType == FundSourceType.SystemFund.ToString())
            {
                totalSystemFundNeeded += 25_000_000m + fund.DepotId * 5_000_000m;
            }
        }

        var systemFundCreatedAt = seed.StartUtc.AddDays(200);
        if (totalSystemFundNeeded > 0)
        {
            var initialRevenue = totalSystemFundNeeded + 100_000_000m;
            _db.SystemFundTransactions.Add(new SystemFundTransaction
            {
                SystemFundId = systemFund.Id,
                TransactionType = SystemFundTransactionType.LiquidationRevenue.ToString(),
                Amount = initialRevenue,
                ReferenceType = "DepotClosure",
                ReferenceId = 0,
                Note = "Nguồn thu thanh lý tài sản đầu kỳ",
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = systemFundCreatedAt
            });
            systemFund.Balance += initialRevenue;
        }

        foreach (var fund in depotFunds)
        {
            var managerId = seed.Managers[fund.DepotId % seed.Managers.Count].Id;
            var fundCreatedAt = seed.StartUtc.AddDays(220 + fund.Id * 3);
            fund.Balance = 0; // Reset for recalculation

            // 1. Allocation
            if (fund.FundSourceType == FundSourceType.SystemFund.ToString())
            {
                var allocationAmount = 25_000_000m + fund.DepotId * 5_000_000m;

                _db.SystemFundTransactions.Add(new SystemFundTransaction
                {
                    SystemFundId = systemFund.Id,
                    TransactionType = SystemFundTransactionType.AllocationToDepot.ToString(),
                    Amount = allocationAmount,
                    ReferenceType = "SystemFund",
                    ReferenceId = fund.Id,
                    Note = $"Cấp vốn cho quỹ kho {fund.DepotId}",
                    CreatedBy = seed.Admins[0].Id,
                    CreatedAt = fundCreatedAt
                });
                systemFund.Balance -= allocationAmount;

                _db.DepotFundTransactions.Add(new DepotFundTransaction
                {
                    DepotFundId = fund.Id,
                    TransactionType = DepotFundTransactionType.Allocation.ToString(),
                    Amount = allocationAmount,
                    ReferenceType = DepotFundReferenceType.SystemFund.ToString(),
                    ReferenceId = systemFund.Id,
                    Note = "Nhận phân bổ từ quỹ hệ thống vào quỹ kho",
                    CreatedBy = seed.Admins[0].Id,
                    CreatedAt = fundCreatedAt
                });
                fund.Balance += allocationAmount;
            }
            else if (fund.FundSourceType == FundSourceType.Campaign.ToString() && fund.FundSourceId.HasValue)
            {
                var disbursementsForFund = seededDisbursements
                    .Where(disbursement => disbursement.DepotId == fund.DepotId
                        && disbursement.FundCampaignId == fund.FundSourceId.Value)
                    .OrderBy(disbursement => disbursement.CreatedAt)
                    .ThenBy(disbursement => disbursement.Id)
                    .ToList();

                foreach (var disbursement in disbursementsForFund)
                {
                    _db.DepotFundTransactions.Add(new DepotFundTransaction
                    {
                        DepotFundId = fund.Id,
                        TransactionType = DepotFundTransactionType.Allocation.ToString(),
                        Amount = disbursement.Amount,
                        ReferenceType = DepotFundReferenceType.CampaignDisbursement.ToString(),
                        ReferenceId = disbursement.Id,
                        Note = disbursement.FundingRequestId.HasValue
                            ? $"Nhận giải ngân từ yêu cầu cấp quỹ #{disbursement.FundingRequestId}"
                            : "Nhận giải ngân từ chiến dịch vào quỹ kho",
                        CreatedBy = disbursement.CreatedBy,
                        CreatedAt = disbursement.CreatedAt
                    });
                    fund.Balance += disbursement.Amount;
                }
            }

            // 2. Deduction (VatInvoice)
            var invoice = vatInvoices.Skip(fund.Id % Math.Max(1, vatInvoices.Count)).FirstOrDefault() ?? vatInvoices.FirstOrDefault();
            if (invoice != null)
            {
                var invoiceAmount = invoice.TotalAmount ?? 0m;
                var deductionAmount = invoiceAmount > 0m ? invoiceAmount : 1_500_000m;
                if (fund.Balance >= deductionAmount)
                {
                    _db.DepotFundTransactions.Add(new DepotFundTransaction
                    {
                        DepotFundId = fund.Id,
                        TransactionType = DepotFundTransactionType.Deduction.ToString(),
                        Amount = deductionAmount,
                        ReferenceType = DepotFundReferenceType.VatInvoice.ToString(),
                        ReferenceId = invoice.Id,
                        Note = "Thanh toán mua bổ sung hàng cứu trợ từ quỹ kho",
                        CreatedBy = managerId,
                        CreatedAt = fundCreatedAt.AddHours(5)
                    });
                    fund.Balance -= deductionAmount;
                }
            }

        }

        await _db.SaveChangesAsync(cancellationToken);
    }


    private static string OrganizationName(int index)
    {
        var names = new[]
        {
            "Nhóm thiện nguyện Hướng về miền Trung",
            "Công ty nước sạch Sông Hương",
            "Hội Chữ thập đỏ Đà Nẵng",
            "Quỹ cộng đồng Bạch Mã",
            "Câu lạc bộ xe bán tải miền Trung",
            "Công ty thiết bị cứu hộ An Tâm",
            "Nhóm Bếp ấm vùng lũ"
        };
        return names[index % names.Length] + (index >= 7 ? $" {index + 1}" : "");
    }

    private static string CampaignName(int index)
    {
        var names = new[]
        {
            "Chiến dịch hỗ trợ bão Noru miền Trung",
            "Chiến dịch lũ sớm Huế - Quảng Trị",
            "Chiến dịch tiếp sức vùng sạt lở Trà My",
            "Chiến dịch nước sạch sau lũ Quảng Ngãi",
            "Chiến dịch áo phao cho vùng ngập sâu"
        };
        return names[index % names.Length] + $" #{index + 1}";
    }

    private static bool IsSeedActiveCampaign(int index) => index is 6 or 9 or 10;

    private static DateOnly ActiveCampaignStartDate(int index, DateOnly anchorDate) => index switch
    {
        6 => anchorDate.AddDays(-50),
        9 => anchorDate.AddDays(-45),
        10 => anchorDate.AddDays(-42),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Campaign is not configured as an active seed campaign.")
    };

    private static DateOnly ActiveCampaignEndDate(int index, DateOnly anchorDate) => index switch
    {
        6 => anchorDate.AddDays(90),
        9 => anchorDate.AddDays(75),
        10 => anchorDate.AddDays(60),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Campaign is not configured as an active seed campaign.")
    };
}
