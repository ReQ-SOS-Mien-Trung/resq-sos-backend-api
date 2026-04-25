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
    IAdminRealtimeHubService adminRealtimeHubService,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<CreateRescueTeamCommandHandler> logger) : IRequestHandler<CreateRescueTeamCommand, int>
{
    public async Task<int> Handle(CreateRescueTeamCommand request, CancellationToken ct)
    {
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, ct)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {request.AssemblyPointId}");

        if (ap.Status == AssemblyPointStatus.Unavailable || ap.Status == AssemblyPointStatus.Closed)
            throw new BadRequestException($"Điểm tập kết {ap.Name} đang ({ap.Status}), không thể tạo đội mới tại đây.");

        var team = RescueTeamModel.Create(
            request.Name,
            request.Type,
            request.AssemblyPointId,
            request.ManagedBy,
            request.MaxMembers);

        team.LoadAssemblyPointName(ap.Name!);

        if (request.Members != null && request.Members.Any())
        {
            var eventCache = new Dictionary<int, (int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate, DateTime? CheckInDeadline)>();

            foreach (var mem in request.Members)
            {
                var user = await userRepository.GetByIdAsync(mem.UserId, ct)
                    ?? throw new NotFoundException($"Không tìm thấy thành viên có ID {mem.UserId}");

                if (!eventCache.TryGetValue(mem.EventId, out var sourceEvent))
                {
                    sourceEvent = await assemblyEventRepository.GetEventByIdAsync(mem.EventId, ct)
                        ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {mem.EventId}.");
                    eventCache[mem.EventId] = sourceEvent;
                }

                if (sourceEvent.AssemblyPointId != request.AssemblyPointId)
                    throw new BadRequestException($"Nhân sự {user.LastName} {user.FirstName} không thuộc điểm tập kết được chọn.");

                if (!Enum.TryParse<AssemblyEventStatus>(sourceEvent.Status, true, out var eventStatus) ||
                    (eventStatus != AssemblyEventStatus.Gathering &&
                     eventStatus != AssemblyEventStatus.Completed &&
                     eventStatus != AssemblyEventStatus.Cancelled))
                {
                    throw new BadRequestException($"Sự kiện tập trung id = {mem.EventId} không hợp lệ để tạo đội.");
                }

                if (user.RoleId != 3)
                    throw new BadRequestException($"Người dùng {user.LastName} {user.FirstName} không phải là nhân sự cứu hộ (Role Rescuer).");

                if (mem.IsLeader && !string.Equals(user.RescuerType?.ToString(), RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new BadRequestException($"Thành viên {user.LastName} {user.FirstName} không thể làm đội trưởng vì không phải là nhân sự nòng cốt (Core Rescuer).");

                if (await teamRepository.IsUserInActiveTeamAsync(mem.UserId, ct))
                    throw new ConflictException($"Nhân sự {user.LastName} {user.FirstName} đang thuộc đội cứu hộ khác.");

                var isCheckedIn = await assemblyEventRepository.IsParticipantCheckedInAsync(mem.EventId, mem.UserId, ct);
                if (!isCheckedIn)
                    throw new BadRequestException($"Nhân sự {user.LastName} {user.FirstName} chưa check-in hợp lệ trong sự kiện tập trung đã chọn.");

                string? roleInTeam = null;

                if (request.Type != RescueTeamType.Mixed)
                {
                    var requiredCategory = request.Type switch
                    {
                        RescueTeamType.Rescue => "RESCUE",
                        RescueTeamType.Medical => "MEDICAL",
                        RescueTeamType.Transportation => "TRANSPORTATION",
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(requiredCategory))
                    {
                        var hasRequired = await teamRepository.HasRequiredAbilityCategoryAsync(mem.UserId, requiredCategory, ct);
                        if (!hasRequired)
                            throw new BadRequestException($"Thành viên {user.LastName} {user.FirstName} không có kỹ năng thuộc nhóm {requiredCategory} để tham gia đội {request.Type}.");

                        roleInTeam = requiredCategory;
                    }
                }
                else
                {
                    roleInTeam = await teamRepository.GetTopAbilityCategoryAsync(mem.UserId, ct);
                }

                team.AddMember(
                    mem.UserId,
                    mem.IsLeader,
                    user.RescuerType?.ToString() ?? "Volunteer",
                    roleInTeam ?? "Thành viên",
                    mem.EventId);
            }
        }

        await teamRepository.CreateAsync(team, ct);

        var memberIds = request.Members?.Select(m => m.UserId).ToList() ?? [];
        if (memberIds.Count > 0)
            await assemblyPointRepository.BulkUpdateRescuerAssemblyPointAsync(memberIds, request.AssemblyPointId, ct);

        await unitOfWork.SaveAsync();

        var createdTeam = await teamRepository.GetByCodeAsync(team.Code, ct);
        var teamId = createdTeam?.Id ?? 0;
        await adminRealtimeHubService.PushRescueTeamUpdateAsync(
            new RESQ.Application.Common.Models.AdminRescueTeamRealtimeUpdate
            {
                EntityId = teamId,
                EntityType = "RescueTeam",
                TeamId = teamId,
                Action = "Created",
                Status = team.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            ct);

        if (memberIds.Count > 0)
        {
            var title = "Thông báo đội cứu hộ";
            var body = $"Bạn đã được phân công vào đội cứu hộ \"{request.Name}\". Vui lòng tập hợp theo hướng dẫn của đội trưởng.";

            foreach (var userId in memberIds)
            {
                try
                {
                    await firebaseService.SendNotificationToUserAsync(
                        userId, title, body, "team_assigned", ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không thể gửi thông báo cho rescuer {UserId}", userId);
                }
            }
        }

        return teamId;
    }
}
