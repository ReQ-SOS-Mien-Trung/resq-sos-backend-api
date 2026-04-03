using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.CancelSosRequest;

public class CancelSosRequestCommandHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<CancelSosRequestCommand, CancelSosRequestResponse>
{
    public async Task<CancelSosRequestResponse> Handle(CancelSosRequestCommand request, CancellationToken cancellationToken)
    {
        var sos = await sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS request với ID: {request.SosRequestId}");

        // Owner or companion can cancel
        if (sos.UserId != request.RequestedByUserId)
        {
            var isCompanion = await companionRepository.IsCompanionAsync(request.SosRequestId, request.RequestedByUserId, cancellationToken);
            if (!isCompanion)
                throw new ForbiddenException("Bạn không có quyền huỷ SOS request này.");
        }

        if (sos.Status != SosRequestStatus.Pending && sos.Status != SosRequestStatus.Assigned)
            throw new BadRequestException($"Không thể huỷ SOS request ở trạng thái {sos.Status}.");

        await sosRequestRepository.UpdateStatusAsync(request.SosRequestId, SosRequestStatus.Cancelled, cancellationToken);
        await unitOfWork.SaveAsync();

        return new CancelSosRequestResponse
        {
            SosRequestId = request.SosRequestId,
            Status = "Cancelled"
        };
    }
}
