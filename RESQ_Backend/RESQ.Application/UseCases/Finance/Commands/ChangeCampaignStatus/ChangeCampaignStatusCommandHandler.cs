using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public class ChangeCampaignStatusCommandHandler : IRequestHandler<ChangeCampaignStatusCommand, bool>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCampaignStatusCommandHandler(IFundCampaignRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ChangeCampaignStatusCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _repository.GetByIdAsync(request.CampaignId, cancellationToken);
        if (campaign == null)
        {
            throw new NotFoundException($"Không tìm thấy chiến dịch ID: {request.CampaignId}");
        }

        // Domain Logic
        campaign.ChangeStatus(request.NewStatus, request.ModifiedBy);

        await _repository.UpdateAsync(campaign, cancellationToken);
        return await _unitOfWork.SaveAsync() > 0;
    }
}
