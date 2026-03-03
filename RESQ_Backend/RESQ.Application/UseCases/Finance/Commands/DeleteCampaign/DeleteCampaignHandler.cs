using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.DeleteCampaign;

public class DeleteCampaignHandler : IRequestHandler<DeleteCampaignCommand, bool>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCampaignHandler(IFundCampaignRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteCampaignCommand request, CancellationToken cancellationToken)
    {
        // 1. Retrieve
        var campaign = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (campaign == null)
        {
            throw new NotFoundException($"Không tìm thấy chiến dịch ID: {request.Id}");
        }

        // 2. Execute Domain Logic (Soft Delete Rules)
        campaign.Delete(request.ModifiedBy);

        // 3. Persist (Update the entity state including IsDeleted flag)
        await _repository.UpdateAsync(campaign, cancellationToken);
        return await _unitOfWork.SaveAsync() > 0;
    }
}
