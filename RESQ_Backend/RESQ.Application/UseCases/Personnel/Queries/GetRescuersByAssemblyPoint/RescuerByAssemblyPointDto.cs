namespace RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;

public class RescuerByAssemblyPointDto
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public string? Address { get; set; }
    public string? Ward { get; set; }
    public string? Province { get; set; }
}
