using MediatR;
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
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    IFirebaseService firebaseService) : IRequestHandler<CreateRescueTeamCommand, int> // Thêm IFirebaseService
{
    public async Task<int> Handle(CreateRescueTeamCommand request, CancellationToken ct)
    {
        var team = RescueTeamModel.Create(request.Name, request.Type, request.AssemblyPointId, request.ManagedBy, request.MaxMembers);

        if (request.Members != null && request.Members.Any())
        {
            foreach (var mem in request.Members)
            {
                var user = await userRepository.GetByIdAsync(mem.UserId, ct)
                    ?? throw new NotFoundException($"Không tìm thấy thành viên có ID {mem.UserId}");

                // Validate Role ID = 3 (Rescuer)
                if (user.RoleId != 3)
                    throw new BadRequestException($"Người dùng {user.FirstName} {user.LastName} không phải là nhân sự cứu hộ (Role Rescuer).");

                // Validate Leader must be Core using the new Enum
                if (mem.IsLeader && !string.Equals(user.RescuerType?.ToString(), RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new BadRequestException($"Thành viên {user.FirstName} {user.LastName} không thể làm đội trưởng vì không phải là nhân sự nòng cốt (Core Rescuer).");

                if (await teamRepository.IsUserInActiveTeamAsync(mem.UserId, ct))
                    throw new ConflictException($"Nhân sự {user.FirstName} {user.LastName} đã tham gia một đội cứu hộ khác.");

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

                team.AddMember(mem.UserId, mem.IsLeader, user.RescuerType?.ToString() ?? "Volunteer", roleInTeam ?? "Thành viên");
            }
        }

        await teamRepository.CreateAsync(team, ct);
        await unitOfWork.SaveAsync();

        var createdTeam = await teamRepository.GetByCodeAsync(team.Code, ct);
        if (createdTeam == null) return 0;

        // Định dạng thông báo Push Noti như yêu cầu
        var notiTitle = "🚨 RESQ – Thông báo khẩn";
        var notiBody = $@"Chúng tôi đã gửi lời mời bạn tham gia đội giải cứu {team.Name} để hỗ trợ hoạt động cứu hộ trong khu vực.
Vui lòng xem chi tiết và xác nhận tham gia trong vòng 24 giờ kể từ khi nhận được thông báo này.

Nếu bạn cần hỗ trợ hoặc có bất kỳ thắc mắc nào, vui lòng liên hệ hotline 0372254905
hoặc truy cập website https://resq-sos-mientrung.vercel.app để biết thêm thông tin.

Chúng tôi chân thành cảm ơn sự hỗ trợ và tinh thần sẵn sàng tham gia cứu hộ của bạn.
– Hệ thống RESQ.";

        // Gửi Email Invitations và Push Notifications
        foreach (var mem in request.Members!)
        {
            var user = await userRepository.GetByIdAsync(mem.UserId, ct);
            if (user != null)
            {
                // 1. Gửi Email (Không bắt buộc throw lỗi nếu lỗi gửi thư)
                if (!string.IsNullOrEmpty(user.Email))
                {
                    await emailService.SendTeamInvitationEmailAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        team.Name,
                        createdTeam.Id,
                        user.Id,
                        ct
                    );
                }

                // 2. Gửi Push Notification (iOS/Android)
                await firebaseService.SendNotificationToUserAsync(user.Id, notiTitle, notiBody, "team_invitation", ct);
            }
        }

        return createdTeam.Id;
    }
}
