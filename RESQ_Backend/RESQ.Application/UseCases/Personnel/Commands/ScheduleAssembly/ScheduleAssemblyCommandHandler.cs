using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Handlers;

public class ScheduleAssemblyCommandHandler(
    IRescueTeamRepository teamRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ScheduleAssemblyCommand>
{
    public async Task Handle(ScheduleAssemblyCommand request, CancellationToken ct)
    {
        var team = await teamRepository.GetByIdAsync(request.TeamId, ct) 
            ?? throw new NotFoundException($"Không tìm thấy đội id = {request.TeamId}");

        team.ScheduleAssembly(request.AssemblyDate);
        await teamRepository.UpdateAsync(team, ct);
        await unitOfWork.SaveAsync();
    }
}
