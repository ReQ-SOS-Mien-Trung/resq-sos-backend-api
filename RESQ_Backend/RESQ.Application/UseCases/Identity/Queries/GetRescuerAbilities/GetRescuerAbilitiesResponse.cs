namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerAbilities;

public class RescuerAbilityDto
{
    public int AbilityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Level { get; set; }
}

public class GetRescuerAbilitiesResponse
{
    public Guid UserId { get; set; }
    public List<RescuerAbilityDto> Abilities { get; set; } = [];
}
