namespace RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;

public class SaveRescuerAbilitiesRequestDto
{
    public List<RescuerAbilityItemDto> Abilities { get; set; } = [];
}

public class RescuerAbilityItemDto
{
    public int AbilityId { get; set; }
    public int? Level { get; set; }
}
