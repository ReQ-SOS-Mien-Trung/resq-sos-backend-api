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
            throw new BadRequestException("Danh sách user ID không du?c d? tr?ng.");

        // 1. Validate assembly point t?n t?i (n?u gán m?i)
        string? apName = null;

        if (!request.AssemblyPointId.HasValue)
        {
            logger.LogInformation("G? s? lu?ng l?n {Count} rescuer kh?i di?m t?p k?t hi?n t?i (chi?u OUT). Thao tác này luôn m? k? c? khi AssemblyPoint Unavailable.", request.UserIds.Count);
        }
        if (request.AssemblyPointId.HasValue)
        {
            var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm th?y di?m t?p k?t v?i id = {request.AssemblyPointId.Value}");

            if (ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Unavailable || ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Closed)
            {
                throw new BadRequestException($"Ði?m t?p k?t {ap.Name} dang tr?ng thái ({ap.Status}), không th? nh?n lu?ng l?n ngu?i lúc này.");
            }

            apName = ap.Name;
        }

        // 2. Validate t?t c? user t?n t?i và có role Rescuer - m?t l?n query
        var users = await userRepository.GetByIdsAsync(request.UserIds, cancellationToken);

        var missingIds = request.UserIds.Except(users.Select(u => u.Id)).ToList();
        if (missingIds.Count > 0)
            throw new NotFoundException($"Không tìm th?y ngu?i dùng v?i ID: {string.Join(", ", missingIds)}");

        var nonRescuers = users.Where(u => u.RoleId != 3).ToList();
        if (nonRescuers.Count > 0)
        {
            var names = string.Join(", ", nonRescuers.Select(u => $"{u.LastName} {u.FirstName}".Trim()));
            throw new BadRequestException($"Ngu?i dùng sau không ph?i nhân s? c?u h?: {names}");
        }

        // 3. Bulk UPDATE assembly point - single SQL statement
        var updatedIds = await assemblyPointRepository.BulkUpdateRescuerAssemblyPointAsync(
            request.UserIds, request.AssemblyPointId, cancellationToken);

        // 4. N?u có AP dang active: t? d?ng thêm rescuer chua có d?i vào event
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
                        "T? d?ng thêm {Count} rescuer vào s? ki?n EventId={EventId} (AP={ApId})",
                        teamlessIds.Count, activeEvent.Value.EventId, request.AssemblyPointId.Value);
                }
            }
        }

        await unitOfWork.SaveAsync();

        // 5. G?i Firebase notification cho t?ng rescuer (song song, không block, không throw)
        // Dùng CancellationToken.None d? notification luôn du?c g?i sau khi SaveAsync() thành công,
        // không b? cancel theo HTTP request. SendNotificationToUserAsync dã catch all exceptions n?i b?.
        var notificationTasks = updatedIds.Select(userId =>
        {
            string title, body;
            if (request.AssemblyPointId.HasValue)
            {
                title = "C?p nh?t di?m t?p k?t";
                body = $"B?n dã du?c ch? d?nh vào di?m t?p k?t \"{apName}\". " +
                       "Vui lòng ki?m tra thông tin chi ti?t trong ?ng d?ng.";
            }
            else
            {
                title = "C?p nh?t di?m t?p k?t";
                body = "B?n dã du?c g? kh?i di?m t?p k?t hi?n t?i. " +
                       "Vui lòng liên h? qu?n tr? viên n?u c?n thêm thông tin.";
            }

            return firebaseService
                .SendNotificationToUserAsync(userId, title, body, "assembly_point_assignment", CancellationToken.None);
        }).ToList();

        await Task.WhenAll(notificationTasks);
    }
}
