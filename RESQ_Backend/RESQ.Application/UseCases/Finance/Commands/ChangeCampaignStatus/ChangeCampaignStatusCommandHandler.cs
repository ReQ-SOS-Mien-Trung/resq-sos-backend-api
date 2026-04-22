using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public class ChangeCampaignStatusCommandHandler : IRequestHandler<ChangeCampaignStatusCommand, bool>
{
    private readonly IFundCampaignRepository _repository;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCampaignStatusCommandHandler(
        IFundCampaignRepository repository,
        IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _adminRealtimeHubService = adminRealtimeHubService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ChangeCampaignStatusCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _repository.GetByIdAsync(request.CampaignId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chiến dịch ID: {request.CampaignId}");

        // Route to the correct semantic domain method based on requested status
        switch (request.NewStatus)
        {
            case FundCampaignStatus.Active when campaign.Status == FundCampaignStatus.Draft:
                campaign.Activate(request.ModifiedBy);
                break;

            case FundCampaignStatus.Active when campaign.Status == FundCampaignStatus.Suspended:
                campaign.Resume(request.ModifiedBy);
                break;

            case FundCampaignStatus.Suspended:
                campaign.Suspend(request.Reason ?? string.Empty, request.ModifiedBy);
                break;

            case FundCampaignStatus.Closed:
                campaign.Close(request.ModifiedBy);
                break;

            case FundCampaignStatus.Archived:
                campaign.Archive(request.ModifiedBy);
                break;

            default:
                throw new BadRequestException(
                    $"Không thể chuyển trạng thái từ '{campaign.Status}' sang '{request.NewStatus}'.");
        }

        await _repository.UpdateAsync(campaign, cancellationToken);
        var changed = await _unitOfWork.SaveAsync() > 0;
        await _adminRealtimeHubService.PushCampaignUpdateAsync(
            new AdminCampaignRealtimeUpdate
            {
                EntityId = campaign.Id,
                EntityType = "Campaign",
                CampaignId = campaign.Id,
                Action = "StatusChanged",
                Status = campaign.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);
        return changed;
    }
}
