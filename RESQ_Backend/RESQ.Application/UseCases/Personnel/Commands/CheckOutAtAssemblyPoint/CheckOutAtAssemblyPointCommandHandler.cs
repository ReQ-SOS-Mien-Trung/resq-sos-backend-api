using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckOutAtAssemblyPoint
{
    public class CheckOutAtAssemblyPointCommandHandler(
        IAssemblyEventRepository assemblyEventRepository,
        IRescueTeamRepository rescueTeamRepository,
        IUserRepository userRepository,
        IFirebaseService firebaseService,
        IUnitOfWork unitOfWork)
        : IRequestHandler<CheckOutAtAssemblyPointCommand>
    {
        public async Task Handle(CheckOutAtAssemblyPointCommand request, CancellationToken cancellationToken)
        {
            var evt = await assemblyEventRepository.GetEventByIdAsync(request.EventId, cancellationToken)
                ?? throw new NotFoundException($"Sự kiện tập trung không tồn tại: {request.EventId}");

            if (evt.Status != AssemblyEventStatus.Gathering.ToString())
                throw new BadRequestException($"Sự kiện không ở trạng thái đang tập hợp. Trạng thái hiện tại: {evt.Status}");

            var success = await assemblyEventRepository.CheckOutAsync(request.EventId, request.RescuerId, cancellationToken);
            if (!success)
                throw new BadRequestException("Bạn chưa check-in hoặc không nằm trong danh sách tham gia.");

            // Lấy leader trước khi soft-remove (vì sau remove status đổi thành Removed, query sẽ không tìm thấy)
            var leaderId = await rescueTeamRepository.GetTeamLeaderUserIdByMemberAsync(request.RescuerId, cancellationToken);

            // Soft-remove rescuer khỏi đội đang tham gia
            await rescueTeamRepository.SoftRemoveMemberFromActiveTeamAsync(request.RescuerId, cancellationToken);

            // Lưu tất cả thay đổi (checkout + remove member) trong 1 lần
            await unitOfWork.SaveAsync();

            // Lấy tên rescuer để đưa vào nội dung thông báo
            var rescuer = await userRepository.GetByIdAsync(request.RescuerId, cancellationToken);
            var rescuerName = rescuer != null
                ? $"{rescuer.FirstName} {rescuer.LastName}".Trim()
                : request.RescuerId.ToString();

            var notifyData = new Dictionary<string, string>
            {
                { "eventId", request.EventId.ToString() },
                { "rescuerId", request.RescuerId.ToString() }
            };

            // Thông báo cho đội trưởng (nếu người check-out không phải chính họ)
            if (leaderId.HasValue && leaderId.Value != request.RescuerId)
            {
                await firebaseService.SendNotificationToUserAsync(
                    leaderId.Value,
                    "Thành viên đã rời điểm tập kết",
                    $"{rescuerName} đã check-out khỏi sự kiện tập trung.",
                    "assembly_checkout",
                    notifyData,
                    cancellationToken);
            }

            // Thông báo cho coordinator (người tạo sự kiện)
            var coordinatorId = await assemblyEventRepository.GetEventCreatedByAsync(request.EventId, cancellationToken);
            if (coordinatorId.HasValue && coordinatorId.Value != request.RescuerId)
            {
                await firebaseService.SendNotificationToUserAsync(
                    coordinatorId.Value,
                    "Thành viên đã rời điểm tập kết",
                    $"{rescuerName} đã check-out khỏi sự kiện tập trung.",
                    "assembly_checkout",
                    notifyData,
                    cancellationToken);
            }
        }
    }
}
