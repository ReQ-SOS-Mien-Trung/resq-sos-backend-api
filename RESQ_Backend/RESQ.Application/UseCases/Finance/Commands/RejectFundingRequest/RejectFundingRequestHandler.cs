using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;

public class RejectFundingRequestHandler : IRequestHandler<RejectFundingRequestCommand, Unit>
{
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;
    private readonly IUnitOfWork _unitOfWork;

    public RejectFundingRequestHandler(
        IFundingRequestRepository fundingRequestRepo,
        IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork)
    {
        _fundingRequestRepo = fundingRequestRepo;
        _adminRealtimeHubService = adminRealtimeHubService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RejectFundingRequestCommand request, CancellationToken cancellationToken)
    {
        var fundingRequest = await _fundingRequestRepo.GetByIdAsync(request.FundingRequestId, cancellationToken)
            ?? throw new RESQ.Application.Exceptions.NotFoundException(
                $"Không tìm thấy yêu cầu cấp quỹ #{request.FundingRequestId}.");

        // Domain logic
        fundingRequest.Reject(request.ReviewedBy, request.Reason);

        await _fundingRequestRepo.UpdateAsync(fundingRequest, cancellationToken);
        await _unitOfWork.SaveAsync();
        await _adminRealtimeHubService.PushFundingRequestUpdateAsync(
            new AdminFundingRequestRealtimeUpdate
            {
                EntityId = fundingRequest.Id,
                EntityType = "FundingRequest",
                RequestId = fundingRequest.Id,
                DepotId = fundingRequest.DepotId,
                Action = "Rejected",
                Status = fundingRequest.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        return Unit.Value;
    }
}
