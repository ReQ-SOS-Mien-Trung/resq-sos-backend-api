namespace RESQ.Domain.Entities.Identity;

public class AbilitySubgroupModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AbilityCategoryId { get; set; }
    public List<AbilityModel> Abilities { get; set; } = [];
}
