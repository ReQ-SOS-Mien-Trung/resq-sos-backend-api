namespace RESQ.Application.UseCases.Emergency.Queries;

public class CompanionResultDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime AddedAt { get; set; }
}
