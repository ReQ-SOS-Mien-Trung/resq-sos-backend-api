namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilityCategories;

public class AbilityItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AbilitySubgroupItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<AbilityItemDto> Abilities { get; set; } = [];
}

public class AbilityCategoryItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<AbilitySubgroupItemDto> Subgroups { get; set; } = [];
}

public class GetAllAbilityCategoriesResponse
{
    public List<AbilityCategoryItemDto> Items { get; set; } = [];
}
