using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateCampaign;

public class CreateCampaignHandler : IRequestHandler<CreateCampaignCommand, int>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCampaignHandler(IFundCampaignRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        // 1. Create Domain Entity (Validation happens in Constructor)
        var campaign = new FundCampaignModel(
            request.Name,
            request.Region,
            request.TargetAmount,
            request.CampaignStartDate,
            request.CampaignEndDate,
            request.CreatedBy
        );

        // 2. Persist
        await _repository.CreateAsync(campaign, cancellationToken);
        await _unitOfWork.SaveAsync();

        return campaign.Id;
    }
}
