using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;

public class ViewAllCampaignsHandler : IRequestHandler<ViewAllCampaignsQuery, PagedResult<CampaignListDto>>
{
    private readonly IFundCampaignRepository _repository;

    public ViewAllCampaignsHandler(IFundCampaignRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<CampaignListDto>> Handle(ViewAllCampaignsQuery request, CancellationToken cancellationToken)
    {
        // Get Domain Models from Repository (which handles sorting and IsDeleted filter)
        var pagedModels = await _repository.GetPagedAsync(request.PageNumber, request.PageSize, request.Statuses, cancellationToken);

        // Map Domain Model to DTO
        var dtos = pagedModels.Items.Select(x => new CampaignListDto
        {
            Id = x.Id,
            Name = x.Name,
            Region = x.Region,
            TargetAmount = x.TargetAmount ?? 0,
            TotalAmount = x.TotalAmount ?? 0,
            Status = x.Status.ToString(),
            CampaignStartDate = x.Duration?.StartDate,
            CampaignEndDate = x.Duration?.EndDate,
            CreatedAt = x.CreatedAt.ToVietnamTime()
        }).ToList();

        return new PagedResult<CampaignListDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
