using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.ViewCampaignMetadata;

public class ViewCampaignMetadataHandler : IRequestHandler<ViewCampaignMetadataQuery, List<MetadataDto>>
{
    private readonly IFundCampaignRepository _repository;

    public ViewCampaignMetadataHandler(IFundCampaignRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<MetadataDto>> Handle(ViewCampaignMetadataQuery request, CancellationToken cancellationToken)
    {
        // Requirement: Only Active and !IsDeleted
        // Using GetPagedAsync with large size or implementing a specific GetList method in repo
        // For metadata usually we don't paginate, but keeping it safe. 
        // Assuming GetAll is needed, here utilizing GetPaged with status filter.
        
        var pagedResult = await _repository.GetPagedAsync(1, 1000, FundCampaignStatus.Active.ToString(), cancellationToken);

        return pagedResult.Items
            .Select(c => new MetadataDto
            {
                Key = c.Id,
                Value = c.Name
            })
            .ToList();
    }
}
