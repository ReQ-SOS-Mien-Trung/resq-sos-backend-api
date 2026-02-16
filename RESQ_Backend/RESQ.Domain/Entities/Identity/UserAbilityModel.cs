namespace RESQ.Domain.Entities.Identity;

public class UserAbilityModel
{
    public Guid UserId { get; set; }
    public int AbilityId { get; set; }
    public int? Level { get; set; }
    public string? AbilityCode { get; set; }
    public string? AbilityDescription { get; set; }
}
