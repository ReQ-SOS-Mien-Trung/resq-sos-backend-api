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
        // 1. Validate rescuer t?n t?i & là role Rescuer
        var user = await userRepository.GetByIdAsync(request.RescuerUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm th?y ngu?i dùng v?i ID = {request.RescuerUserId}");

        if (user.RoleId != 3)
            throw new BadRequestException($"Ngu?i dùng {user.LastName} {user.FirstName} không ph?i là nhân s? c?u h?.");

        string? apName = null;

        if (!request.AssemblyPointId.HasValue)
        {
            logger.LogInformation("Th?c hi?n g? rescuer {UserId} kh?i di?m t?p k?t hi?n t?i (chi?u OUT). Thao tác này luôn du?c phép th?c hi?n d? gi?i phóng nhân s? dù di?m t?p k?t có dang Unavailable hay không.", request.RescuerUserId);
        }

        // 2. Validate di?m t?p k?t t?n t?i (n?u gán m?i)
        if (request.AssemblyPointId.HasValue)
        {
            var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm th?y di?m t?p k?t v?i id = {request.AssemblyPointId.Value}");

            if (ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Unavailable || ap.Status == Domain.Enum.Personnel.AssemblyPointStatus.Closed)
            {
                throw new BadRequestException($"Ði?m t?p k?t {ap.Name} dang ({ap.Status}), không th? nh?n ngu?i lúc này.");
            }

            apName = ap.Name;
        }

        // 3. C?p nh?t assembly point cho rescuer
        await assemblyPointRepository.UpdateRescuerAssemblyPointAsync(
            request.RescuerUserId, request.AssemblyPointId, cancellationToken);

        // 3b. N?u AP có s? ki?n t?p trung dang active ? t? d?ng thêm rescuer vào danh sách participant
        //     CH? khi rescuer chua thu?c d?i c?u h? nào (tri?u t?p d? x?p nhóm)
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
                        "T? d?ng thêm rescuer {UserId} vào s? ki?n t?p trung EventId={EventId} (AP={ApId})",
                        request.RescuerUserId, activeEvent.Value.EventId, request.AssemblyPointId.Value);
                }
            }
            else
            {
                logger.LogInformation(
                    "Rescuer {UserId} dã có d?i c?u h? — b? qua tri?u t?p t?i AP {ApId}",
                    request.RescuerUserId, request.AssemblyPointId.Value);
            }
        }

        await unitOfWork.SaveAsync();

        // 4. G?i thông báo Firebase cho rescuer
        try
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

            await firebaseService.SendNotificationToUserAsync(
                request.RescuerUserId, title, body, "assembly_point_assignment", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không th? g?i thông báo cho rescuer {UserId}", request.RescuerUserId);
        }
    }
}
