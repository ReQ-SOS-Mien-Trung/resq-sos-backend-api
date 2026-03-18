using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;

public class RejectFundingRequestHandler : IRequestHandler<RejectFundingRequestCommand, Unit>
{
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RejectFundingRequestHandler(
        IFundingRequestRepository fundingRequestRepo,
        IUnitOfWork unitOfWork)
    {
        _fundingRequestRepo = fundingRequestRepo;
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

        return Unit.Value;
    }
}
