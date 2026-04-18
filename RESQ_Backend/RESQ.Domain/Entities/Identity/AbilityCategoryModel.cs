namespace RESQ.Domain.Entities.Identity;

public class AbilityCategoryModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<AbilitySubgroupModel> Subgroups { get; set; } = [];

    public static AbilityCategoryModel Create(string code, string? description) => new()
    {
        Code = code,
        Description = description
    };

    public void Update(string code, string? description)
    {
        Code = code;
        Description = description;
    }
}
