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
        // Validate AP tồn tại
        var ap = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, ct)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {request.AssemblyPointId}");

        // Tạo đội ở trạng thái Gathering (rescuer đã có mặt tại AP)
        var team = RescueTeamModel.Create(
            request.Name, request.Type, request.AssemblyPointId, request.ManagedBy, request.MaxMembers);

        if (request.Members != null && request.Members.Any())
        {
            foreach (var mem in request.Members)
            {
                var user = await userRepository.GetByIdAsync(mem.UserId, ct)
                    ?? throw new NotFoundException($"Không tìm thấy thành viên có ID {mem.UserId}");

                // Validate Role ID = 3 (Rescuer)
                if (user.RoleId != 3)
                    throw new BadRequestException($"Người dùng {user.FirstName} {user.LastName} không phải là nhân sự cứu hộ (Role Rescuer).");

                // Validate Leader must be Core
                if (mem.IsLeader && !string.Equals(user.RescuerType?.ToString(), RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new BadRequestException($"Thành viên {user.FirstName} {user.LastName} không thể làm đội trưởng vì không phải là nhân sự nòng cốt (Core Rescuer).");

                if (await teamRepository.IsUserInActiveTeamAsync(mem.UserId, ct))
                    throw new ConflictException($"Nhân sự {user.FirstName} {user.LastName} đã tham gia một đội cứu hộ khác.");

                // Validate rescuer đã check-in tại sự kiện tập trung
                var isCheckedIn = await assemblyEventRepository.IsParticipantCheckedInAsync(request.AssemblyEventId, mem.UserId, ct);
                if (!isCheckedIn)
                    throw new BadRequestException($"Nhân sự {user.FirstName} {user.LastName} chưa check-in tại điểm tập kết.");

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
                            throw new BadRequestException($"Thành viên {user.FirstName} {user.LastName} không có kỹ năng thuộc nhóm {requiredCategory} để tham gia đội {request.Type}.");

                        roleInTeam = requiredCategory;
                    }
                }
                else
                {
                    roleInTeam = await teamRepository.GetTopAbilityCategoryAsync(mem.UserId, ct);
                }

                // Thêm member ở trạng thái Accepted (đã có mặt tại AP)
                team.AddMember(mem.UserId, mem.IsLeader, user.RescuerType?.ToString() ?? "Volunteer", roleInTeam ?? "Thành viên");
            }
        }

        await teamRepository.CreateAsync(team, ct);
        await unitOfWork.SaveAsync();

        var createdTeam = await teamRepository.GetByCodeAsync(team.Code, ct);
        var teamId = createdTeam?.Id ?? 0;

        // Gửi thông báo cho tất cả rescuer trong đội
        var memberIds = request.Members?.Select(m => m.UserId).ToList() ?? [];
        if (memberIds.Count > 0)
        {
            var title = "Thông báo đội cứu hộ";
            var body = $"Bạn đã được phân công vào đội cứu hộ \"{request.Name}\". " +
                       "Vui lòng tập hợp theo hướng dẫn của đội trưởng.";

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
