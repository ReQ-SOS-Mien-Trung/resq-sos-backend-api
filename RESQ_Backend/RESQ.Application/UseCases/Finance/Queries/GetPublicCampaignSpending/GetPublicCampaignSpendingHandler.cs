using MediatR;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// Public handler for donors/contributors to see campaign spending grouped by depot.
/// </summary>
public class GetPublicCampaignSpendingHandler : IRequestHandler<GetPublicCampaignSpendingQuery, PublicCampaignSpendingDto>
{
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotInventoryRepository _inventoryRepo;

    public GetPublicCampaignSpendingHandler(
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo,
        IDepotFundRepository depotFundRepo,
        IDepotInventoryRepository inventoryRepo)
    {
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
        _depotFundRepo = depotFundRepo;
        _inventoryRepo = inventoryRepo;
    }

    public async Task<PublicCampaignSpendingDto> Handle(GetPublicCampaignSpendingQuery request, CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepo.GetByIdAsync(request.CampaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Khong tim thay chien dich #{request.CampaignId}.");

        var disbursements = await _disbursementRepo.GetPublicByCampaignAsync(request.CampaignId, cancellationToken);
        var totalDisbursed = disbursements.Sum(x => x.Amount);
        var campaignFunds = await _depotFundRepo.GetFundsByCampaignAsync(request.CampaignId, cancellationToken);
        var depotFundIds = campaignFunds.Select(x => x.FundId).Distinct().ToList();
        var purchasedItems = await _inventoryRepo.GetPurchasedItemsByDepotFundIdsAsync(depotFundIds, cancellationToken);

        var allocatedByDepot = disbursements
            .GroupBy(x => x.DepotId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var allocationsByDepot = disbursements
            .GroupBy(x => x.DepotId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Select(x => new PublicDepotCampaignAllocationDto
                    {
                        Id = x.Id,
                        Amount = x.Amount,
                        Purpose = x.Purpose,
                        Type = x.Type.ToString(),
                        FundingRequestId = x.FundingRequestId,
                        AllocatedAt = ToVietnamTime(x.CreatedAt)
                    })
                    .ToList());

        var depotGroups = campaignFunds
            .GroupBy(x => new { x.DepotId, x.DepotName })
            .Select(g => new PublicDepotCampaignSpendingDto
            {
                DepotId = g.Key.DepotId,
                DepotName = g.Key.DepotName,
                TotalAllocated = allocatedByDepot.GetValueOrDefault(g.Key.DepotId),
                Allocations = allocationsByDepot.GetValueOrDefault(g.Key.DepotId) ?? [],
                TotalSpent = purchasedItems.Where(x => x.DepotId == g.Key.DepotId).Sum(x => x.TotalPrice),
                Imports = purchasedItems
                    .Where(x => x.DepotId == g.Key.DepotId)
                    .GroupBy(x => new
                    {
                        x.DepotFundId,
                        x.VatInvoiceId,
                        x.InvoiceSerial,
                        x.InvoiceNumber,
                        x.SupplierName,
                        x.InvoiceDate,
                        ImportBatchAt = x.VatInvoiceId.HasValue ? null : x.ImportedAt
                    })
                    .Select(import => new PublicDepotCampaignImportDto
                    {
                        DepotFundId = import.Key.DepotFundId,
                        VatInvoiceId = import.Key.VatInvoiceId,
                        InvoiceSerial = import.Key.InvoiceSerial,
                        InvoiceNumber = import.Key.InvoiceNumber,
                        SupplierName = import.Key.SupplierName,
                        InvoiceDate = import.Key.InvoiceDate,
                        InvoiceTotalAmount = import.Sum(i => i.TotalPrice),
                        ImportedAt = import.Max(i => i.ImportedAt),
                        TotalSpent = import.Sum(i => i.TotalPrice),
                        Items = import
                            .GroupBy(i => new
                            {
                                i.ItemName,
                                i.Unit,
                                i.UnitPrice,
                                i.ItemType
                            })
                            .Select(item => new PublicDepotCampaignPurchasedItemDto
                            {
                                ItemName = item.Key.ItemName,
                                Unit = item.Key.Unit,
                                Quantity = item.Sum(i => i.Quantity),
                                UnitPrice = item.Key.UnitPrice,
                                TotalPrice = item.Sum(i => i.TotalPrice),
                                ReceivedDate = item.Min(i => i.ReceivedDate),
                                ExpiredDate = item.Min(i => i.ExpiredDate),
                                ItemType = item.Key.ItemType
                            })
                            .OrderByDescending(i => i.TotalPrice)
                            .ThenBy(i => i.ItemName)
                            .ToList()
                    })
                    .OrderByDescending(x => x.ImportedAt)
                    .ThenByDescending(x => x.VatInvoiceId)
                    .ToList()
            })
            .OrderByDescending(x => x.TotalAllocated)
            .ThenBy(x => x.DepotName)
            .ToList();

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var pagedDepots = depotGroups
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PublicCampaignSpendingDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            TotalRaised = campaign.TotalAmount ?? 0m,
            TotalDisbursed = totalDisbursed,
            RemainingBalance = campaign.CurrentBalance ?? 0m,
            Depots = pagedDepots,
            TotalCount = depotGroups.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static DateTime ToVietnamTime(DateTime utcTime)
        => DateTime.SpecifyKind(utcTime.AddHours(7), DateTimeKind.Unspecified);
}
