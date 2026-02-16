namespace RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;

public class SaveRescuerAbilitiesResponse
{
    public Guid UserId { get; set; }
    public int SavedCount { get; set; }
    public List<SavedAbilityDto> Abilities { get; set; } = [];
}

public class SavedAbilityDto
{
    public int AbilityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Level { get; set; }
}
