using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Finance.Commands.DeleteCampaign;

public class DeleteCampaignHandler : IRequestHandler<DeleteCampaignCommand, bool>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCampaignHandler(
        IFundCampaignRepository repository,
        IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _adminRealtimeHubService = adminRealtimeHubService;
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
        var deleted = await _unitOfWork.SaveAsync() > 0;
        await _adminRealtimeHubService.PushCampaignUpdateAsync(
            new AdminCampaignRealtimeUpdate
            {
                EntityId = campaign.Id,
                EntityType = "Campaign",
                CampaignId = campaign.Id,
                Action = "Deleted",
                Status = campaign.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
        return deleted;
    }
}
