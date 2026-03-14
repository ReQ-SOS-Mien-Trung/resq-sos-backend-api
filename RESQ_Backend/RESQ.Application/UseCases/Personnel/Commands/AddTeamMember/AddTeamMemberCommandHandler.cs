using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class AddTeamMemberCommandHandler(
    IRescueTeamRepository teamRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService) : IRequestHandler<AddTeamMemberCommand>
{
    public async Task Handle(AddTeamMemberCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {request.UserId}");

        if (user.RoleId != 3)
            throw new BadRequestException($"Người dùng {user.FirstName} {user.LastName} không phải là nhân sự cứu hộ (Role Rescuer).");

        if (request.IsLeader && !string.Equals(user.RescuerType?.ToString(), RescuerType.Core.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException($"Thành viên {user.FirstName} {user.LastName} không thể làm đội trưởng vì không phải là nhân sự nòng cốt (Core Rescuer).");

        if (await teamRepository.IsUserInActiveTeamAsync(request.UserId, ct))
            throw new ConflictException("Nhân sự này đã tham gia một đội cứu hộ khác.");

        string? roleInTeam = null;

        if (team.TeamType != RescueTeamType.Mixed)
        {
            string requiredCategory = team.TeamType switch
            {
                RescueTeamType.Rescue => "RESCUE",
                RescueTeamType.Medical => "MEDICAL",
                RescueTeamType.Transportation => "TRANSPORTATION",
                _ => ""
            };

            if (!string.IsNullOrEmpty(requiredCategory))
            {
                bool hasRequired = await teamRepository.HasRequiredAbilityCategoryAsync(request.UserId, requiredCategory, ct);
                if (!hasRequired)
                    throw new BadRequestException($"Thành viên không có kỹ năng thuộc nhóm {requiredCategory} để tham gia đội {team.TeamType}.");

                roleInTeam = requiredCategory;
            }
        }
        else
        {
            roleInTeam = await teamRepository.GetTopAbilityCategoryAsync(request.UserId, ct);
        }

        team.AddMember(request.UserId, request.IsLeader, user.RescuerType?.ToString() ?? "Volunteer", roleInTeam ?? "Thành viên");
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();

        // Send Email Invitation
        if (!string.IsNullOrEmpty(user.Email))
        {
            await emailService.SendTeamInvitationEmailAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}",
                team.Name,
                team.Id,
                user.Id,
                ct
            );
        }
    }
}