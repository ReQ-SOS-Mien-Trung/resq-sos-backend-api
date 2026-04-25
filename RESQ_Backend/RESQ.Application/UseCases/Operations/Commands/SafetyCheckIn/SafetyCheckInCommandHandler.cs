using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SafetyCheckIn;

public class SafetyCheckInCommandHandler : IRequestHandler<SafetyCheckInCommand, bool>
{
    private readonly IMissionTeamRepository _missionTeamRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly IMissionActivityRepository _missionActivityRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SafetyCheckInCommandHandler(
        IMissionTeamRepository missionTeamRepository,
        IMissionRepository missionRepository,
        IMissionActivityRepository missionActivityRepository,
        IUnitOfWork unitOfWork)
    {
        _missionTeamRepository = missionTeamRepository;
        _missionRepository = missionRepository;
        _missionActivityRepository = missionActivityRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(SafetyCheckInCommand request, CancellationToken cancellationToken)
    {
        var missionTeam = await _missionTeamRepository.GetByIdAsync(request.TeamId, cancellationToken);
        if (missionTeam == null || missionTeam.MissionId != request.MissionId)
        {
            throw new NotFoundException($"Không tìm thấy đội trong nhiệm vụ với id {request.TeamId}.");
        }

        var isMember = missionTeam.RescueTeamMembers.Any(m => m.UserId == request.UserId);
        if (!isMember)
        {
            throw new ForbiddenException("Bạn không phải là thành viên của đội cứu hộ này nên không thể báo cáo an toàn.");
        }

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
            
        if (mission == null)
        {
            throw new NotFoundException($"Không tìm thấy nhiệm vụ {request.MissionId}.");
        }

        if (mission.Status != MissionStatus.OnGoing)
        {
            throw new BadRequestException("Chỉ có thể check-in an toàn khi nhiệm vụ đang diễn ra.");
        }

        var activities = await _missionActivityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        MissionTeamSafetyHelper.ExtendSafetyTimeout(missionTeam, activities);

        await _unitOfWork.SaveAsync();

        return true;
    }
}
