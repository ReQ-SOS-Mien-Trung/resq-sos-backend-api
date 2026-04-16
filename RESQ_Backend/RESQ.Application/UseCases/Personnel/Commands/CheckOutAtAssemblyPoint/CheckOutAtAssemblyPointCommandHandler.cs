using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;
using System.Collections.Generic;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckOutAtAssemblyPoint
{
    public class CheckOutAtAssemblyPointCommandHandler(
        IAssemblyEventRepository assemblyEventRepository,
        IAssemblyPointRepository assemblyPointRepository,
        IRescueTeamRepository rescueTeamRepository,
        IUserRepository userRepository,
        IFirebaseService firebaseService,
        IOperationalHubService operationalHubService,
        IUnitOfWork unitOfWork)
        : IRequestHandler<CheckOutAtAssemblyPointCommand>
    {
        public async Task Handle(CheckOutAtAssemblyPointCommand request, CancellationToken cancellationToken)
        {
            var evt = await assemblyEventRepository.GetEventByIdAsync(request.EventId, cancellationToken)
                ?? throw new NotFoundException($"Sự kiện tập trung không tồn tại: {request.EventId}");

            if (evt.Status != AssemblyEventStatus.Gathering.ToString())
                throw new BadRequestException($"Sự kiện không ở trạng thái đang tập hợp. Trạng thái hiện tại: {evt.Status}");

            // Validate: rescuer phải rời nhóm trước khi check-out
            var stillInTeam = await rescueTeamRepository.IsUserInActiveTeamAsync(request.RescuerId, cancellationToken);
            if (stillInTeam)
                throw new BadRequestException("Bạn cần rời khỏi đội cứu hộ trước khi thực hiện check-out khỏi sự kiện tập trung.");

            var success = await assemblyEventRepository.CheckOutAsync(request.EventId, request.RescuerId, cancellationToken);
            if (!success)
                throw new BadRequestException("Bạn chưa check-in hoặc không nằm trong danh sách tham gia.");

            // Gỡ rescuer khỏi điểm tập kết (rời hẳn, không chỉ rời sự kiện)
            await assemblyPointRepository.UpdateRescuerAssemblyPointAsync(request.RescuerId, null, cancellationToken);

            await unitOfWork.SaveAsync();
            await operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken);

            // Lấy tên rescuer để đưa vào nội dung thông báo
            var rescuer = await userRepository.GetByIdAsync(request.RescuerId, cancellationToken);
            var rescuerName = rescuer != null
                ? $"{rescuer.LastName} {rescuer.FirstName}".Trim()
                : request.RescuerId.ToString();

            // Thông báo cho coordinator (người tạo sự kiện)
            var coordinatorId = await assemblyEventRepository.GetEventCreatedByAsync(request.EventId, cancellationToken);
            if (coordinatorId.HasValue && coordinatorId.Value != request.RescuerId)
            {
                await firebaseService.SendNotificationToUserAsync(
                    coordinatorId.Value,
                    "Thành viên đã rời điểm tập kết",
                    $"{rescuerName} đã check-out khỏi sự kiện tập trung.",
                    "assembly_checkout",
                    new Dictionary<string, string>
                    {
                        { "eventId", request.EventId.ToString() },
                        { "rescuerId", request.RescuerId.ToString() }
                    },
                    cancellationToken);
            }
        }
    }
}

