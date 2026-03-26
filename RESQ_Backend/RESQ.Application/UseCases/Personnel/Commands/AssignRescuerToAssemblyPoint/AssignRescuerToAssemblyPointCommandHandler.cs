using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

public class AssignRescuerToAssemblyPointCommandHandler(
    IUserRepository userRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<AssignRescuerToAssemblyPointCommandHandler> logger)
    : IRequestHandler<AssignRescuerToAssemblyPointCommand>
{
    public async Task Handle(AssignRescuerToAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate rescuer tồn tại & là role Rescuer
        var user = await userRepository.GetByIdAsync(request.RescuerUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {request.RescuerUserId}");

        if (user.RoleId != 3)
            throw new BadRequestException($"Người dùng {user.FirstName} {user.LastName} không phải là nhân sự cứu hộ.");

        string? apName = null;

        // 2. Validate điểm tập kết tồn tại (nếu gán mới)
        if (request.AssemblyPointId.HasValue)
        {
            var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId.Value}");

            apName = ap.Name;
        }

        // 3. Cập nhật assembly point cho rescuer
        await assemblyPointRepository.UpdateRescuerAssemblyPointAsync(
            request.RescuerUserId, request.AssemblyPointId, cancellationToken);

        // 3b. Nếu AP có sự kiện tập trung đang active → tự động thêm rescuer vào danh sách participant
        //     CHỈ khi rescuer chưa thuộc đội cứu hộ nào (triệu tập để xếp nhóm)
        if (request.AssemblyPointId.HasValue)
        {
            var hasTeam = await assemblyPointRepository.HasActiveTeamAsync(
                request.RescuerUserId, cancellationToken);

            if (!hasTeam)
            {
                var activeEvent = await assemblyEventRepository.GetActiveEventByAssemblyPointAsync(
                    request.AssemblyPointId.Value, cancellationToken);

                if (activeEvent != null)
                {
                    await assemblyEventRepository.AssignParticipantsAsync(
                        activeEvent.Value.EventId, [request.RescuerUserId], cancellationToken);

                    logger.LogInformation(
                        "Tự động thêm rescuer {UserId} vào sự kiện tập trung EventId={EventId} (AP={ApId})",
                        request.RescuerUserId, activeEvent.Value.EventId, request.AssemblyPointId.Value);
                }
            }
            else
            {
                logger.LogInformation(
                    "Rescuer {UserId} đã có đội cứu hộ — bỏ qua triệu tập tại AP {ApId}",
                    request.RescuerUserId, request.AssemblyPointId.Value);
            }
        }

        await unitOfWork.SaveAsync();

        // 4. Gửi thông báo Firebase cho rescuer
        try
        {
            string title, body;

            if (request.AssemblyPointId.HasValue)
            {
                title = "Cập nhật điểm tập kết";
                body = $"Bạn đã được chỉ định vào điểm tập kết \"{apName}\". " +
                       "Vui lòng kiểm tra thông tin chi tiết trong ứng dụng.";
            }
            else
            {
                title = "Cập nhật điểm tập kết";
                body = "Bạn đã được gỡ khỏi điểm tập kết hiện tại. " +
                       "Vui lòng liên hệ quản trị viên nếu cần thêm thông tin.";
            }

            await firebaseService.SendNotificationToUserAsync(
                request.RescuerUserId, title, body, "assembly_point_assignment", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể gửi thông báo cho rescuer {UserId}", request.RescuerUserId);
        }
    }
}
