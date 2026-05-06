using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateUserAbilities
{
    public record AbilityEntry(int AbilityId, int? Level);

    public record UpdateUserAbilitiesCommand(
        Guid CallerUserId,
        Guid TargetUserId,
        List<AbilityEntry> Abilities
    ) : IRequest<UpdateUserAbilitiesResponse>;
}
