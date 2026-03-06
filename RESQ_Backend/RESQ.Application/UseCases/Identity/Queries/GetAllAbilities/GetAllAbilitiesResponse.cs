namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilities;

public class AbilityCategoryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AbilitySubgroupDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AbilityCategoryId { get; set; }
    public AbilityCategoryDto? AbilityCategory { get; set; }
}

public class AbilityDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AbilitySubgroupId { get; set; }
    public AbilitySubgroupDto? AbilitySubgroup { get; set; }
}

public class GetAllAbilitiesResponse
{
    public List<AbilityDto> Items { get; set; } = [];
}
