using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignTeamsToAssemblyPoint;

public class AssignTeamsToAssemblyPointCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AssignTeamsToAssemblyPointCommand>
{
    public async Task Handle(AssignTeamsToAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy và validate điểm tập kết
        var assemblyPoint = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        // 2. Đếm số đội hiện tại (trừ những đội trong request có thể đang thuộc AP này rồi)
        var currentTeamCount = await rescueTeamRepository.CountActiveTeamsByAssemblyPointAsync(
            request.AssemblyPointId,
            request.TeamIds,
            cancellationToken);

        // 3. Tính số đội mới thực sự cần thêm (loại trừ đội đã thuộc AP này)
        var teams = new List<RESQ.Domain.Entities.Personnel.RescueTeamModel>();
        var newTeamsToAdd = 0;

        foreach (var teamId in request.TeamIds)
        {
            var team = await rescueTeamRepository.GetByIdAsync(teamId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với id = {teamId}");

            // Business rule: không thể gán đội đang thuộc AP khác
            if (team.AssemblyPointId != 0 && team.AssemblyPointId != request.AssemblyPointId)
                throw new ConflictException(
                    $"Đội \"{team.Name}\" (id={teamId}) đang thuộc điểm tập kết khác (id={team.AssemblyPointId}). Vui lòng gỡ khỏi điểm tập kết hiện tại trước.");

            // Nếu đội chưa thuộc AP này mới tính là "mới thêm"
            if (team.AssemblyPointId != request.AssemblyPointId)
                newTeamsToAdd++;

            teams.Add(team);
        }

        // 4. Domain validation: sức chứa + trạng thái AP
        assemblyPoint.ValidateTeamAssignment(currentTeamCount, newTeamsToAdd);

        // 5. Gán từng đội vào AP và persist
        foreach (var team in teams)
        {
            if (team.AssemblyPointId != request.AssemblyPointId)
            {
                team.AssignToAssemblyPoint(request.AssemblyPointId);
                await rescueTeamRepository.UpdateAsync(team, cancellationToken);
            }
        }

        await unitOfWork.SaveAsync();
    }
}
