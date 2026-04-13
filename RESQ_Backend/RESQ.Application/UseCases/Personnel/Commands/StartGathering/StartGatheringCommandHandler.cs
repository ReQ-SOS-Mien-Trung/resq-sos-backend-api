using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.StartGathering;

public class StartGatheringCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IUnitOfWork unitOfWork,
    ILogger<StartGatheringCommandHandler> logger)
    : IRequestHandler<StartGatheringCommand>
{
    public async Task Handle(StartGatheringCommand request, CancellationToken cancellationToken)
    {
        var evt = await assemblyEventRepository.GetEventByIdAsync(request.AssemblyEventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.AssemblyEventId}");

        var ap = await assemblyPointRepository.GetByIdAsync(evt.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {evt.AssemblyPointId}");

        if (ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Unavailable || ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Closed)
        {
            throw new BadRequestException($"Không thể bắt đầu sự kiện tập trung vì điểm tập kết {ap.Name} đang ở trạng thái {ap.Status}.");
        }

        logger.LogInformation("StartGathering for EventId={EventId}, current status={Status}",
            request.AssemblyEventId, evt.Status);

        // Domain rule: Scheduled → Gathering (enforce trong repository)
        await assemblyEventRepository.StartGatheringAsync(request.AssemblyEventId, cancellationToken);
        await unitOfWork.SaveAsync();
    }
}
