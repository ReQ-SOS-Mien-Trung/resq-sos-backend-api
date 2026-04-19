using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CompleteMissionTeamExecution;

public class CompleteMissionTeamExecutionCommandHandler(
    IMissionTeamRepository missionTeamRepository,
    IRescueTeamMissionLifecycleSyncService rescueTeamMissionLifecycleSyncService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompleteMissionTeamExecutionCommand, CompleteMissionTeamExecutionResponse>
{
    public async Task<CompleteMissionTeamExecutionResponse> Handle(CompleteMissionTeamExecutionCommand request, CancellationToken cancellationToken)
    {
        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team không thuộc mission được yêu cầu.");

        if (!missionTeam.RescueTeamMembers.Any(x => x.UserId == request.CompletedBy))
            throw new ForbiddenException("Bạn không phải thành viên của đội cứu hộ này.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Đội đã bị hủy phân công, không thể hoàn tất thực thi.");

        if (string.Equals(missionTeam.Status, MissionTeamExecutionStatus.Reported.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Đội này đã nộp báo cáo cuối cùng.");

        var nextStatus = MissionTeamExecutionStatus.CompletedWaitingReport.ToString();
        await missionTeamRepository.UpdateStatusAsync(request.MissionTeamId, nextStatus, request.Note, cancellationToken);

        var rescueTeamLifecycleSyncResult =
            await rescueTeamMissionLifecycleSyncService.SyncTeamToAvailableAfterExecutionAsync(
                missionTeam.RescuerTeamId,
                request.MissionTeamId,
                cancellationToken);

        if (rescueTeamLifecycleSyncResult.HasChanges)
        {
            await unitOfWork.SaveAsync();
            await rescueTeamMissionLifecycleSyncService.PushRealtimeIfNeededAsync(
                rescueTeamLifecycleSyncResult,
                cancellationToken);
        }

        return new CompleteMissionTeamExecutionResponse
        {
            MissionId = request.MissionId,
            MissionTeamId = request.MissionTeamId,
            Status = nextStatus,
            Note = request.Note
        };
    }
}
