using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;

public record SaveRescuerAbilitiesCommand(
    Guid UserId,
    List<RescuerAbilityItem> Abilities
) : IRequest<SaveRescuerAbilitiesResponse>;

public class RescuerAbilityItem
{
    public int AbilityId { get; set; }
    public int? Level { get; set; }
}
