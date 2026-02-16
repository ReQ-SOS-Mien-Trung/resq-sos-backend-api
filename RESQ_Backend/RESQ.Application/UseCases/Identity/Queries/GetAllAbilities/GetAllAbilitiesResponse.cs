namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilities;

public class AbilityDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class GetAllAbilitiesResponse
{
    public List<AbilityDto> Items { get; set; } = [];
}
