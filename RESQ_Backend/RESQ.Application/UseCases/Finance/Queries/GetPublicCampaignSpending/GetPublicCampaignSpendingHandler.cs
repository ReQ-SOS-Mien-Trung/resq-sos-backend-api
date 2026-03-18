using MediatR;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// Handler công khai cho donor xem chi tiêu campaign.
/// </summary>
public class GetPublicCampaignSpendingHandler : IRequestHandler<GetPublicCampaignSpendingQuery, PublicCampaignSpendingDto>
{
    private readonly IFundCampaignRepository _campaignRepo;
    private readonly ICampaignDisbursementRepository _disbursementRepo;

    public GetPublicCampaignSpendingHandler(
        IFundCampaignRepository campaignRepo,
        ICampaignDisbursementRepository disbursementRepo)
    {
        _campaignRepo = campaignRepo;
        _disbursementRepo = disbursementRepo;
    }

    public async Task<PublicCampaignSpendingDto> Handle(GetPublicCampaignSpendingQuery request, CancellationToken cancellationToken)
    {
        // 1. Lấy Campaign
        var campaign = await _campaignRepo.GetByIdAsync(request.CampaignId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy chiến dịch #{request.CampaignId}.");

        // 2. Lấy tổng giải ngân
        var totalDisbursed = await _disbursementRepo.GetTotalDisbursedByCampaignAsync(request.CampaignId, cancellationToken);

        // 3. Lấy danh sách disbursement (kèm items) — public view
        var pagedDisbursements = await _disbursementRepo.GetPublicByCampaignAsync(
            request.CampaignId, request.PageNumber, request.PageSize, cancellationToken);

        // 4. Map to DTO
        var dto = new PublicCampaignSpendingDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            TotalRaised = campaign.TotalAmount ?? 0,
            TotalDisbursed = totalDisbursed,
            RemainingBalance = (campaign.TotalAmount ?? 0) - totalDisbursed,
            TotalCount = pagedDisbursements.TotalCount,
            PageNumber = pagedDisbursements.PageNumber,
            PageSize = pagedDisbursements.PageSize,
            Disbursements = pagedDisbursements.Items.Select(d => new PublicDisbursementDto
            {
                Id = d.Id,
                DepotName = d.DepotName,
                Amount = d.Amount,
                Purpose = d.Purpose,
                Type = d.Type.ToString(),
                CreatedAt = d.CreatedAt,
                Items = d.Items.Select(i => new PublicDisbursementItemDto
                {
                    ItemName = i.ItemName,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            }).ToList()
        };

        return dto;
    }
}
