namespace RESQ.Application.Common.Models;

public class MissionActivityTargetVictimDto
{
    public string? PersonId { get; set; }
    public string? DisplayName { get; set; }
    public string? PersonType { get; set; }
    public string? PersonPhone { get; set; }
    public int? Index { get; set; }
    public bool? IsInjured { get; set; }
    public string? Severity { get; set; }
    public List<string> MedicalIssues { get; set; } = [];
    public bool? ClothingNeeded { get; set; }
    public string? ClothingGender { get; set; }
    public string? SpecialDietDescription { get; set; }
}
