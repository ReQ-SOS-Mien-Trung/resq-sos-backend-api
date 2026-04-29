using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Maintenance;

public sealed class SeedDataSyncService(
    ResQDbContext dbContext,
    ILogger<SeedDataSyncService> logger) : ISeedDataSyncService
{
    private readonly ResQDbContext _dbContext = dbContext;
    private readonly ILogger<SeedDataSyncService> _logger = logger;

    public async Task<SeedDataSyncReport> SyncAsync(
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var report = new SeedDataSyncReport
        {
            DryRun = dryRun,
            GeneratedAt = DateTime.UtcNow
        };

        await RecomputeCampaignsAsync(report, dryRun, cancellationToken);
        await RecomputeDepotFundsAsync(report, dryRun, cancellationToken);
        await RecomputeInventoryAsync(report, dryRun, cancellationToken);
        RecomputeDerivedStates(report);

        DeduplicateAffectedIds(report);
        return report;
    }

    private async Task RecomputeCampaignsAsync(
        SeedDataSyncReport report,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var campaigns = await Query<FundCampaign>(dryRun)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        report.Campaigns.Scanned = campaigns.Count;

        var successfulDonationStatus = Status.Succeed.ToString();
        var donationTotals = await _dbContext.Donations
            .AsNoTracking()
            .Where(x => x.FundCampaignId.HasValue
                        && x.Status == successfulDonationStatus
                        && x.Amount.HasValue)
            .GroupBy(x => x.FundCampaignId!.Value)
            .Select(g => new { CampaignId = g.Key, Total = g.Sum(x => x.Amount ?? 0m) })
            .ToDictionaryAsync(x => x.CampaignId, x => x.Total, cancellationToken);

        var disbursementTotals = await _dbContext.CampaignDisbursements
            .AsNoTracking()
            .GroupBy(x => x.FundCampaignId)
            .Select(g => new { CampaignId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CampaignId, x => x.Total, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var campaign in campaigns)
        {
            var totalDonation = donationTotals.GetValueOrDefault(campaign.Id);
            var totalDisbursement = disbursementTotals.GetValueOrDefault(campaign.Id);
            var expectedBalance = totalDonation - totalDisbursement;

            var changed = false;
            if ((campaign.TotalAmount ?? 0m) != totalDonation)
            {
                AddChange(
                    report.Campaigns,
                    "FundCampaign",
                    campaign.Id.ToString(),
                    nameof(FundCampaign.TotalAmount),
                    campaign.TotalAmount,
                    totalDonation,
                    "Recomputed from successful donations.");
                changed = true;

                if (!dryRun)
                {
                    campaign.TotalAmount = totalDonation;
                }
            }

            if ((campaign.CurrentBalance ?? 0m) != expectedBalance)
            {
                AddChange(
                    report.Campaigns,
                    "FundCampaign",
                    campaign.Id.ToString(),
                    nameof(FundCampaign.CurrentBalance),
                    campaign.CurrentBalance,
                    expectedBalance,
                    "Recomputed as successful donations minus campaign disbursements.");
                changed = true;

                if (!dryRun)
                {
                    campaign.CurrentBalance = expectedBalance;
                }
            }

            if (expectedBalance < 0)
            {
                report.Warnings.Add(
                    $"Campaign #{campaign.Id} computed current balance is negative ({expectedBalance:N2}). Source donation/disbursement data should be reviewed.");
            }

            if (campaign.Status == FundCampaignStatus.Active.ToString()
                && campaign.CampaignEndDate.HasValue
                && campaign.CampaignEndDate.Value < today)
            {
                AddChange(
                    report.DerivedStates,
                    "FundCampaign",
                    campaign.Id.ToString(),
                    nameof(FundCampaign.Status),
                    campaign.Status,
                    FundCampaignStatus.Closed,
                    "Active campaign is past campaign_end_date.");
                report.DerivedStates.Changed++;
                changed = true;

                if (!dryRun)
                {
                    campaign.Status = FundCampaignStatus.Closed.ToString();
                }
            }

            if (!changed)
            {
                continue;
            }

            report.Campaigns.Changed++;
            report.AffectedCampaignIds.Add(campaign.Id);
            if (!dryRun)
            {
                campaign.LastModifiedAt = report.GeneratedAt;
            }
        }
    }

    private async Task RecomputeDepotFundsAsync(
        SeedDataSyncReport report,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var depotFunds = await Query<DepotFund>(dryRun)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        report.DepotFunds.Scanned = depotFunds.Count;

        var transactions = await _dbContext.DepotFundTransactions
            .AsNoTracking()
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var transactionsByFund = transactions
            .GroupBy(x => x.DepotFundId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var fund in depotFunds)
        {
            var computedBalance = 0m;
            var skippedTransaction = false;

            foreach (var transaction in transactionsByFund.GetValueOrDefault(fund.Id) ?? [])
            {
                if (!TryGetDepotFundTransactionSign(transaction.TransactionType, out var sign))
                {
                    skippedTransaction = true;
                    report.Warnings.Add(
                        $"DepotFund #{fund.Id} transaction #{transaction.Id} has unsupported type '{transaction.TransactionType}', so the fund was skipped.");
                    continue;
                }

                computedBalance += sign * transaction.Amount;
            }

            if (skippedTransaction)
            {
                report.DepotFunds.Skipped++;
                continue;
            }

            if (computedBalance < 0)
            {
                report.DepotFunds.Skipped++;
                report.Warnings.Add(
                    $"DepotFund #{fund.Id} computed balance is negative ({computedBalance:N2}); skipped update to avoid violating depot fund balance rules.");
                continue;
            }

            if (fund.Balance == computedBalance)
            {
                continue;
            }

            AddChange(
                report.DepotFunds,
                "DepotFund",
                fund.Id.ToString(),
                nameof(DepotFund.Balance),
                fund.Balance,
                computedBalance,
                "Recomputed from depot fund transactions.");

            report.DepotFunds.Changed++;
            report.AffectedDepotFundIds.Add(fund.Id);
            report.AffectedDepotFundDepotIds.Add(fund.DepotId);

            if (!dryRun)
            {
                fund.Balance = computedBalance;
                fund.LastUpdatedAt = report.GeneratedAt;
            }
        }

        await RecomputeDepotAdvanceDebtAsync(report, dryRun, transactions, cancellationToken);
    }

    private async Task RecomputeDepotAdvanceDebtAsync(
        SeedDataSyncReport report,
        bool dryRun,
        List<DepotFundTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var fundsById = await _dbContext.DepotFunds
            .AsNoTracking()
            .Select(x => new { x.Id, x.DepotId })
            .ToDictionaryAsync(x => x.Id, x => x.DepotId, cancellationToken);

        var advanceByDepot = new Dictionary<int, decimal>();
        foreach (var transaction in transactions)
        {
            if (!fundsById.TryGetValue(transaction.DepotFundId, out var depotId))
            {
                continue;
            }

            if (!DepotFundTransactionTypeAlias.TryParse(transaction.TransactionType, out var type))
            {
                continue;
            }

            var delta = type switch
            {
                DepotFundTransactionType.PersonalAdvance => transaction.Amount,
                DepotFundTransactionType.AdvanceRepayment => -transaction.Amount,
                _ => 0m
            };

            if (delta == 0m)
            {
                continue;
            }

            advanceByDepot[depotId] = advanceByDepot.GetValueOrDefault(depotId) + delta;
        }

        var depots = await Query<Depot>(dryRun)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var depot in depots)
        {
            var expectedDebt = advanceByDepot.GetValueOrDefault(depot.Id);
            if (expectedDebt < 0)
            {
                report.DerivedStates.Skipped++;
                report.Warnings.Add(
                    $"Depot #{depot.Id} computed outstanding advance is negative ({expectedDebt:N2}); skipped update.");
                continue;
            }

            if (depot.OutstandingAdvanceAmount == expectedDebt)
            {
                continue;
            }

            AddChange(
                report.DerivedStates,
                "Depot",
                depot.Id.ToString(),
                nameof(Depot.OutstandingAdvanceAmount),
                depot.OutstandingAdvanceAmount,
                expectedDebt,
                "Recomputed from PersonalAdvance and AdvanceRepayment depot fund transactions.");

            report.DerivedStates.Changed++;
            report.AffectedDepotIds.Add(depot.Id);

            if (expectedDebt > depot.AdvanceLimit)
            {
                report.Warnings.Add(
                    $"Depot #{depot.Id} outstanding advance ({expectedDebt:N2}) exceeds advance limit ({depot.AdvanceLimit:N2}).");
            }

            if (!dryRun)
            {
                depot.OutstandingAdvanceAmount = expectedDebt;
                depot.LastUpdatedAt = report.GeneratedAt;
            }
        }
    }

    private async Task RecomputeInventoryAsync(
        SeedDataSyncReport report,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var inventories = await Query<SupplyInventory>(dryRun)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        report.Inventory.Scanned = inventories.Count;

        var lotSummaries = await _dbContext.SupplyInventoryLots
            .AsNoTracking()
            .GroupBy(x => x.SupplyInventoryId)
            .Select(g => new
            {
                SupplyInventoryId = g.Key,
                LotCount = g.Count(),
                RemainingQuantity = g.Sum(x => x.RemainingQuantity),
                TotalQuantity = g.Sum(x => x.Quantity)
            })
            .ToDictionaryAsync(x => x.SupplyInventoryId, cancellationToken);

        var movementQuantities = await BuildInventoryMovementQuantitiesAsync(report, cancellationToken);
        var transferReservedQuantities = await BuildTransferReservedQuantitiesAsync(cancellationToken);

        var computedQuantityByInventory = new Dictionary<int, int>();

        foreach (var inventory in inventories)
        {
            var changed = false;
            var hasLotSummary = lotSummaries.TryGetValue(inventory.Id, out var lotSummary);
            var hasMovementQuantity = movementQuantities.TryGetValue(inventory.Id, out var movementQuantity);

            int? expectedQuantity = null;
            if (hasLotSummary)
            {
                expectedQuantity = Math.Max(0, lotSummary!.RemainingQuantity);
                if (hasMovementQuantity && movementQuantity != expectedQuantity.Value)
                {
                    report.Warnings.Add(
                        $"SupplyInventory #{inventory.Id} lot remaining total ({expectedQuantity.Value}) differs from movement-derived quantity ({movementQuantity}); lot total was used.");
                }
            }
            else if (hasMovementQuantity)
            {
                expectedQuantity = Math.Max(0, movementQuantity);
            }
            else
            {
                report.Inventory.Skipped++;
                report.Warnings.Add(
                    $"SupplyInventory #{inventory.Id} has no lots and no supported inventory movement logs; quantity was not recomputed.");
            }

            if (expectedQuantity.HasValue)
            {
                computedQuantityByInventory[inventory.Id] = expectedQuantity.Value;
                if ((inventory.Quantity ?? 0) != expectedQuantity.Value)
                {
                    AddChange(
                        report.Inventory,
                        "SupplyInventory",
                        inventory.Id.ToString(),
                        nameof(SupplyInventory.Quantity),
                        inventory.Quantity,
                        expectedQuantity,
                        hasLotSummary
                            ? "Recomputed from inventory lot remaining quantities."
                            : "Recomputed from inventory movement logs.");
                    changed = true;

                    if (!dryRun)
                    {
                        inventory.Quantity = expectedQuantity.Value;
                    }
                }
            }

            var expectedTransferReserved = transferReservedQuantities.GetValueOrDefault(inventory.Id);
            if (inventory.TransferReservedQuantity != expectedTransferReserved)
            {
                AddChange(
                    report.Inventory,
                    "SupplyInventory",
                    inventory.Id.ToString(),
                    nameof(SupplyInventory.TransferReservedQuantity),
                    inventory.TransferReservedQuantity,
                    expectedTransferReserved,
                    "Recomputed from active transfer/closure consumable reservations.");
                changed = true;

                if (!dryRun)
                {
                    inventory.TransferReservedQuantity = expectedTransferReserved;
                }
            }

            if (inventory.MissionReservedQuantity > 0)
            {
                report.Warnings.Add(
                    $"SupplyInventory #{inventory.Id} has mission_reserved_quantity={inventory.MissionReservedQuantity}; no normalized mission reservation source was found, so this field was preserved.");
            }

            var quantityForDeletedRule = expectedQuantity ?? (inventory.Quantity ?? 0);
            var expectedDeleted = quantityForDeletedRule <= 0
                                  && inventory.MissionReservedQuantity == 0
                                  && expectedTransferReserved == 0;
            if (inventory.IsDeleted != expectedDeleted)
            {
                AddChange(
                    report.DerivedStates,
                    "SupplyInventory",
                    inventory.Id.ToString(),
                    nameof(SupplyInventory.IsDeleted),
                    inventory.IsDeleted,
                    expectedDeleted,
                    "Derived from quantity and reserved quantities.");
                changed = true;

                if (!dryRun)
                {
                    inventory.IsDeleted = expectedDeleted;
                }
            }

            if (changed)
            {
                report.Inventory.Changed++;
                AddAffectedDepot(report, inventory.DepotId);
                if (!dryRun)
                {
                    inventory.LastStockedAt ??= report.GeneratedAt;
                }
            }
        }

        await RecomputeDepotUtilizationAsync(report, dryRun, computedQuantityByInventory, cancellationToken);
    }

    private async Task<Dictionary<int, int>> BuildInventoryMovementQuantitiesAsync(
        SeedDataSyncReport report,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, int>();
        var logs = await _dbContext.InventoryLogs
            .AsNoTracking()
            .Where(x => x.DepotSupplyInventoryId.HasValue && x.QuantityChange.HasValue)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var log in logs)
        {
            if (!TryGetInventoryMovementSign(log.ActionType, log.QuantityChange.GetValueOrDefault(), out var signedQuantity))
            {
                report.Warnings.Add(
                    $"InventoryLog #{log.Id} has unsupported action_type '{log.ActionType}', so it was ignored for movement-based recompute.");
                continue;
            }

            var inventoryId = log.DepotSupplyInventoryId.GetValueOrDefault();
            result[inventoryId] = result.GetValueOrDefault(inventoryId) + signedQuantity;
        }

        return result;
    }

    private async Task<Dictionary<int, int>> BuildTransferReservedQuantitiesAsync(
        CancellationToken cancellationToken)
    {
        var supplyRequestReservations = await _dbContext.DepotSupplyRequestConsumableReservations
            .AsNoTracking()
            .Where(x => x.Status == "Reserved")
            .GroupBy(x => x.SupplyInventoryId)
            .Select(g => new { SupplyInventoryId = g.Key, Quantity = g.Sum(x => x.ReservedQuantity) })
            .ToListAsync(cancellationToken);

        var closureReservations = await _dbContext.DepotClosureTransferConsumableReservations
            .AsNoTracking()
            .Where(x => x.Status == "Reserved")
            .GroupBy(x => x.SupplyInventoryId)
            .Select(g => new { SupplyInventoryId = g.Key, Quantity = g.Sum(x => x.ReservedQuantity) })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, int>();
        foreach (var row in supplyRequestReservations)
        {
            result[row.SupplyInventoryId] = result.GetValueOrDefault(row.SupplyInventoryId) + row.Quantity;
        }

        foreach (var row in closureReservations)
        {
            result[row.SupplyInventoryId] = result.GetValueOrDefault(row.SupplyInventoryId) + row.Quantity;
        }

        return result;
    }

    private async Task RecomputeDepotUtilizationAsync(
        SeedDataSyncReport report,
        bool dryRun,
        Dictionary<int, int> computedQuantityByInventory,
        CancellationToken cancellationToken)
    {
        var depots = await Query<Depot>(dryRun)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var inventoryLoads = await _dbContext.SupplyInventories
            .AsNoTracking()
            .Where(x => x.DepotId.HasValue && x.ItemModelId.HasValue && !x.IsDeleted)
            .Join(
                _dbContext.ItemModels.AsNoTracking(),
                inventory => inventory.ItemModelId,
                item => item.Id,
                (inventory, item) => new
                {
                    InventoryId = inventory.Id,
                    DepotId = inventory.DepotId!.Value,
                    Quantity = inventory.Quantity ?? 0,
                    VolumePerUnit = item.VolumePerUnit ?? 0m,
                    WeightPerUnit = item.WeightPerUnit ?? 0m
                })
            .ToListAsync(cancellationToken);

        var reusableLoads = await _dbContext.ReusableItems
            .AsNoTracking()
            .Where(x => x.DepotId.HasValue
                        && !x.IsDeleted
                        && x.Status != ReusableItemStatus.Decommissioned.ToString()
                        && x.ItemModelId.HasValue)
            .Join(
                _dbContext.ItemModels.AsNoTracking(),
                reusable => reusable.ItemModelId,
                item => item.Id,
                (reusable, item) => new
                {
                    DepotId = reusable.DepotId!.Value,
                    Volume = item.VolumePerUnit ?? 0m,
                    Weight = item.WeightPerUnit ?? 0m
                })
            .ToListAsync(cancellationToken);

        var loadByDepot = new Dictionary<int, (decimal Volume, decimal Weight)>();

        foreach (var row in inventoryLoads)
        {
            var quantity = computedQuantityByInventory.GetValueOrDefault(row.InventoryId, row.Quantity);
            AddLoad(loadByDepot, row.DepotId, quantity * row.VolumePerUnit, quantity * row.WeightPerUnit);
        }

        foreach (var row in reusableLoads)
        {
            AddLoad(loadByDepot, row.DepotId, row.Volume, row.Weight);
        }

        foreach (var depot in depots)
        {
            var load = loadByDepot.GetValueOrDefault(depot.Id);
            var expectedVolume = decimal.Round(load.Volume, 3, MidpointRounding.AwayFromZero);
            var expectedWeight = decimal.Round(load.Weight, 3, MidpointRounding.AwayFromZero);
            var changed = false;

            if ((depot.CurrentUtilization ?? 0m) != expectedVolume)
            {
                AddChange(
                    report.Inventory,
                    "Depot",
                    depot.Id.ToString(),
                    nameof(Depot.CurrentUtilization),
                    depot.CurrentUtilization,
                    expectedVolume,
                    "Recomputed from inventory quantities and reusable units.");
                changed = true;

                if (!dryRun)
                {
                    depot.CurrentUtilization = expectedVolume;
                }
            }

            if ((depot.CurrentWeightUtilization ?? 0m) != expectedWeight)
            {
                AddChange(
                    report.Inventory,
                    "Depot",
                    depot.Id.ToString(),
                    nameof(Depot.CurrentWeightUtilization),
                    depot.CurrentWeightUtilization,
                    expectedWeight,
                    "Recomputed from inventory quantities and reusable units.");
                changed = true;

                if (!dryRun)
                {
                    depot.CurrentWeightUtilization = expectedWeight;
                }
            }

            if (expectedVolume > (depot.Capacity ?? 0m))
            {
                report.Warnings.Add(
                    $"Depot #{depot.Id} recomputed volume utilization ({expectedVolume:N3}) exceeds capacity ({depot.Capacity:N3}).");
            }

            if (expectedWeight > (depot.WeightCapacity ?? 0m))
            {
                report.Warnings.Add(
                    $"Depot #{depot.Id} recomputed weight utilization ({expectedWeight:N3}) exceeds weight capacity ({depot.WeightCapacity:N3}).");
            }

            if (!changed)
            {
                continue;
            }

            report.Inventory.Changed++;
            report.AffectedDepotIds.Add(depot.Id);
            if (!dryRun)
            {
                depot.LastUpdatedAt = report.GeneratedAt;
            }
        }
    }

    private void RecomputeDerivedStates(SeedDataSyncReport report)
    {
        report.Warnings.Add(
            "Reusable item statuses were not recomputed because current state cannot be derived safely from logs without replaying mission and transfer workflows.");

        _logger.LogInformation(
            "Seed data sync prepared. DryRun={DryRun}, HasChanges={HasChanges}, Warnings={WarningCount}",
            report.DryRun,
            report.HasChanges,
            report.Warnings.Count);
    }

    private IQueryable<TEntity> Query<TEntity>(bool dryRun) where TEntity : class
        => dryRun ? _dbContext.Set<TEntity>().AsNoTracking() : _dbContext.Set<TEntity>();

    private static void AddChange(
        SeedDataSyncSectionReport section,
        string entity,
        string entityId,
        string field,
        object? oldValue,
        object? newValue,
        string reason)
    {
        section.Changes.Add(new SeedDataSyncChange
        {
            Entity = entity,
            EntityId = entityId,
            Field = field,
            OldValue = FormatValue(oldValue),
            NewValue = FormatValue(newValue),
            Reason = reason
        });
    }

    private static string? FormatValue(object? value)
        => value switch
        {
            null => null,
            decimal decimalValue => decimalValue.ToString("0.###"),
            DateTime dateTimeValue => dateTimeValue.ToString("O"),
            DateOnly dateOnlyValue => dateOnlyValue.ToString("O"),
            _ => value.ToString()
        };

    private static bool TryGetDepotFundTransactionSign(string? transactionType, out int sign)
    {
        sign = 0;
        if (!DepotFundTransactionTypeAlias.TryParse(transactionType, out var type))
        {
            return false;
        }

        sign = type switch
        {
            DepotFundTransactionType.Allocation => 1,
            DepotFundTransactionType.Refund => 1,
            DepotFundTransactionType.LiquidationRevenue => 1,
            DepotFundTransactionType.PersonalAdvance => 1,
            DepotFundTransactionType.Deduction => -1,
            DepotFundTransactionType.ClosureFundReturn => -1,
            DepotFundTransactionType.AdvanceRepayment => -1,
            _ => 0
        };

        return sign != 0;
    }

    private static bool TryGetInventoryMovementSign(
        string? actionType,
        int quantityChange,
        out int signedQuantity)
    {
        signedQuantity = 0;
        if (!Enum.TryParse<InventoryActionType>(actionType, ignoreCase: true, out var action))
        {
            return false;
        }

        signedQuantity = action switch
        {
            InventoryActionType.Import => Math.Abs(quantityChange),
            InventoryActionType.TransferIn => Math.Abs(quantityChange),
            InventoryActionType.Return => Math.Abs(quantityChange),
            InventoryActionType.Export => -Math.Abs(quantityChange),
            InventoryActionType.TransferOut => -Math.Abs(quantityChange),
            InventoryActionType.MissionPickup => -Math.Abs(quantityChange),
            InventoryActionType.DepotClosureExternalDisposal => -Math.Abs(quantityChange),
            InventoryActionType.DepotClosureReusableDecommissioned => 0,
            InventoryActionType.Reserve => 0,
            InventoryActionType.Adjust => quantityChange,
            _ => 0
        };

        return true;
    }

    private static void AddAffectedDepot(SeedDataSyncReport report, int? depotId)
    {
        if (depotId.HasValue)
        {
            report.AffectedDepotIds.Add(depotId.Value);
        }
    }

    private static void AddLoad(
        Dictionary<int, (decimal Volume, decimal Weight)> loadByDepot,
        int depotId,
        decimal volume,
        decimal weight)
    {
        var current = loadByDepot.GetValueOrDefault(depotId);
        loadByDepot[depotId] = (current.Volume + volume, current.Weight + weight);
    }

    private static void DeduplicateAffectedIds(SeedDataSyncReport report)
    {
        report.AffectedCampaignIds = report.AffectedCampaignIds.Distinct().OrderBy(x => x).ToList();
        report.AffectedDepotIds = report.AffectedDepotIds.Distinct().OrderBy(x => x).ToList();
        report.AffectedDepotFundIds = report.AffectedDepotFundIds.Distinct().OrderBy(x => x).ToList();
        report.AffectedDepotFundDepotIds = report.AffectedDepotFundDepotIds.Distinct().OrderBy(x => x).ToList();
    }
}
