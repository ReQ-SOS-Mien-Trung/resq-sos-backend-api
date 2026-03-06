namespace RESQ.Domain.Entities.Identity;

public class AbilityModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AbilityCategoryId { get; set; }
    public AbilityCategoryModel? AbilityCategory { get; set; }
}
