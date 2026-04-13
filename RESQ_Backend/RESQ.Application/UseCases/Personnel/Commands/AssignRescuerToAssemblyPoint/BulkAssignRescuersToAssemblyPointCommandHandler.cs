using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

public class BulkAssignRescuersToAssemblyPointCommandHandler(
    IUserRepository userRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<BulkAssignRescuersToAssemblyPointCommandHandler> logger)
    : IRequestHandler<BulkAssignRescuersToAssemblyPointCommand>
{
    public async Task Handle(BulkAssignRescuersToAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0)
            throw new BadRequestException("Danh sách user ID không được để trống.");

        // 1. Validate assembly point tồn tại (nếu gán mới)
        string? apName = null;
        if (request.AssemblyPointId.HasValue)
        {
            var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId.Value}");
            apName = ap.Name;
        }

        // 2. Validate tất cả user tồn tại và có role Rescuer - một lần query
        var users = await userRepository.GetByIdsAsync(request.UserIds, cancellationToken);

        var missingIds = request.UserIds.Except(users.Select(u => u.Id)).ToList();
        if (missingIds.Count > 0)
            throw new NotFoundException($"Không tìm thấy người dùng với ID: {string.Join(", ", missingIds)}");

        var nonRescuers = users.Where(u => u.RoleId != 3).ToList();
        if (nonRescuers.Count > 0)
        {
            var names = string.Join(", ", nonRescuers.Select(u => $"{u.LastName} {u.FirstName}".Trim()));
            throw new BadRequestException($"Người dùng sau không phải nhân sự cứu hộ: {names}");
        }

        // 3. Bulk UPDATE assembly point - single SQL statement
        var updatedIds = await assemblyPointRepository.BulkUpdateRescuerAssemblyPointAsync(
            request.UserIds, request.AssemblyPointId, cancellationToken);

        // 4. Nếu có AP đang active: tự động thêm rescuer chưa có đội vào event
        if (request.AssemblyPointId.HasValue && updatedIds.Count > 0)
        {
            var teamlessIds = await assemblyPointRepository.FilterUsersWithoutActiveTeamAsync(
                updatedIds, cancellationToken);

            if (teamlessIds.Count > 0)
            {
                var activeEvent = await assemblyEventRepository.GetActiveEventByAssemblyPointAsync(
                    request.AssemblyPointId.Value, cancellationToken);

                if (activeEvent != null)
                {
                    await assemblyEventRepository.AssignParticipantsAsync(
                        activeEvent.Value.EventId, teamlessIds, cancellationToken);

                    logger.LogInformation(
                        "Tự động thêm {Count} rescuer vào sự kiện EventId={EventId} (AP={ApId})",
                        teamlessIds.Count, activeEvent.Value.EventId, request.AssemblyPointId.Value);
                }
            }
        }

        await unitOfWork.SaveAsync();

        // 5. Gửi Firebase notification cho từng rescuer (song song, không block, không throw)
        // Dùng CancellationToken.None để notification luôn được gửi sau khi SaveAsync() thành công,
        // không bị cancel theo HTTP request. SendNotificationToUserAsync đã catch all exceptions nội bộ.
        var notificationTasks = updatedIds.Select(userId =>
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

            return firebaseService
                .SendNotificationToUserAsync(userId, title, body, "assembly_point_assignment", CancellationToken.None);
        }).ToList();

        await Task.WhenAll(notificationTasks);
    }
}
