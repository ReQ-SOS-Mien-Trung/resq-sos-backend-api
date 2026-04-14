using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class CreateRescueTeamCommandHandler(
    IRescueTeamRepository teamRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<CreateRescueTeamCommandHandler> logger) : IRequestHandler<CreateRescueTeamCommand, int>
{
    public async Task<int> Handle(CreateRescueTeamCommand request, CancellationToken ct)
    {
        // Validate AP t?n t?i
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, ct)
            ?? throw new NotFoundException($"Không tìm th?y di?m t?p k?t id = {request.AssemblyPointId}");

        // T? d?ng tìm event dang Gathering t?i AP d? validate check-in
        if (ap.Status == AssemblyPointStatus.Unavailable || ap.Status == AssemblyPointStatus.Closed)
            throw new BadRequestException($"?i?m t?p k?t {ap.Name} dang ({ap.Status}), kh?ng th? t?o d?i m?i t?i d?y.");

        var activeEvent = await assemblyEventRepository.GetActiveEventByAssemblyPointAsync(request.AssemblyPointId, ct)
            ?? throw new BadRequestException($"Ði?m t?p k?t id = {request.AssemblyPointId} hi?n không có s? ki?n t?p trung dang di?n ra.");
        var resolvedEventId = activeEvent.EventId;

        // T?o d?i ? tr?ng thái Gathering (rescuer dã có m?t t?i AP)
        var team = RescueTeamModel.Create(
            request.Name, request.Type, request.AssemblyPointId, request.ManagedBy, request.MaxMembers);

        team.LoadAssemblyPointName(ap.Name!);

        if (request.Members != null && request.Members.Any())
        {
            foreach (var mem in request.Members)
            {
                var user = await userRepository.GetByIdAsync(mem.UserId, ct)
                    ?? throw new NotFoundException($"Không tìm th?y thành viên có ID {mem.UserId}");

                // Validate Role ID = 3 (Rescuer)
                if (user.RoleId != 3)
                    throw new BadRequestException($"Ngu?i dùng {user.LastName} {user.FirstName} không ph?i là nhân s? c?u h? (Role Rescuer).");

                // Validate Leader must be Core
                if (mem.IsLeader && !string.Equals(user.RescuerType?.ToString(), RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new BadRequestException($"Thành viên {user.LastName} {user.FirstName} không th? làm d?i tru?ng vì không ph?i là nhân s? nòng c?t (Core Rescuer).");

                if (await teamRepository.IsUserInActiveTeamAsync(mem.UserId, ct))
                    throw new ConflictException($"Nhân s? {user.LastName} {user.FirstName} dã tham gia m?t d?i c?u h? khác.");

                // Validate rescuer dã check-in t?i s? ki?n t?p trung
                var isCheckedIn = await assemblyEventRepository.IsParticipantCheckedInAsync(resolvedEventId, mem.UserId, ct);
                if (!isCheckedIn)
                    throw new BadRequestException($"Nhân s? {user.LastName} {user.FirstName} chua check-in t?i di?m t?p k?t.");

                string? roleInTeam = null;

                if (request.Type != RescueTeamType.Mixed)
                {
                    string requiredCategory = request.Type switch
                    {
                        RescueTeamType.Rescue => "RESCUE",
                        RescueTeamType.Medical => "MEDICAL",
                        RescueTeamType.Transportation => "TRANSPORTATION",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(requiredCategory))
                    {
                        bool hasRequired = await teamRepository.HasRequiredAbilityCategoryAsync(mem.UserId, requiredCategory, ct);
                        if (!hasRequired)
                            throw new BadRequestException($"Thành viên {user.LastName} {user.FirstName} không có k? nang thu?c nhóm {requiredCategory} d? tham gia d?i {request.Type}.");

                        roleInTeam = requiredCategory;
                    }
                }
                else
                {
                    roleInTeam = await teamRepository.GetTopAbilityCategoryAsync(mem.UserId, ct);
                }

                // Thêm member ? tr?ng thái Accepted (dã có m?t t?i AP)
                team.AddMember(mem.UserId, mem.IsLeader, user.RescuerType?.ToString() ?? "Volunteer", roleInTeam ?? "Thành viên");
            }
        }

        await teamRepository.CreateAsync(team, ct);
        await unitOfWork.SaveAsync();

        var createdTeam = await teamRepository.GetByCodeAsync(team.Code, ct);
        var teamId = createdTeam?.Id ?? 0;

        // G?i thông báo cho t?t c? rescuer trong d?i
        var memberIds = request.Members?.Select(m => m.UserId).ToList() ?? [];
        if (memberIds.Count > 0)
        {
            var title = "Thông báo d?i c?u h?";
            var body = $"B?n dã du?c phân công vào d?i c?u h? \"{request.Name}\". " +
                       "Vui lòng t?p h?p theo hu?ng d?n c?a d?i tru?ng.";

            foreach (var userId in memberIds)
            {
                try
                {
                    await firebaseService.SendNotificationToUserAsync(
                        userId, title, body, "team_assigned", ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không th? g?i thông báo cho rescuer {UserId}", userId);
                }
            }
        }

        return teamId;
    }
}
