using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Finance.Commands.ExtendCampaign;

public class ExtendCampaignCommandHandler : IRequestHandler<ExtendCampaignCommand, bool>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendCampaignCommandHandler(
        IFundCampaignRepository repository,
        IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _adminRealtimeHubService = adminRealtimeHubService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ExtendCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _repository.GetByIdAsync(request.CampaignId, cancellationToken);
        if (campaign == null)
        {
            throw new NotFoundException($"Không tìm thấy chiến dịch ID: {request.CampaignId}");
        }

        // Domain Logic
        campaign.ExtendDuration(request.NewEndDate, request.ModifiedBy);

        await _repository.UpdateAsync(campaign, cancellationToken);
        var updated = await _unitOfWork.SaveAsync() > 0;
        await _adminRealtimeHubService.PushCampaignUpdateAsync(
            new AdminCampaignRealtimeUpdate
            {
                EntityId = campaign.Id,
                EntityType = "Campaign",
                CampaignId = campaign.Id,
                Action = "Extended",
                Status = campaign.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
        return updated;
    }
}
