using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;

public class CheckInAtAssemblyPointCommandHandler(
    IAssemblyEventRepository assemblyEventRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CheckInAtAssemblyPointCommand>
{
    public async Task Handle(CheckInAtAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate sự kiện tồn tại
        var evt = await assemblyEventRepository.GetEventByIdAsync(request.AssemblyEventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.AssemblyEventId}");

        // 2. Validate trạng thái event phải là Gathering (đã mở check-in)
        if (evt.Status != AssemblyEventStatus.Gathering.ToString())
            throw new BadRequestException(
                $"Sự kiện tập trung chưa mở check-in. Trạng thái hiện tại: {evt.Status}.");

        // 3. Check-in (validate participant tồn tại + idempotent)
        var success = await assemblyEventRepository.CheckInAsync(
            request.AssemblyEventId, request.UserId, cancellationToken);

        if (!success)
            throw new BadRequestException("Bạn không nằm trong danh sách tham gia sự kiện tập trung này.");

        await unitOfWork.SaveAsync();
    }
}
