namespace RESQ.Application.UseCases.Identity.Commands.CreateAbilityCategory;

public class CreateAbilityCategoryResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}
