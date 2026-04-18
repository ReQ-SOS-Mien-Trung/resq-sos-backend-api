namespace RESQ.Domain.Entities.Identity;

public class UserAbilityModel
{
    public Guid UserId { get; set; }
    public int AbilityId { get; set; }
    public int? Level { get; set; }
    public string? AbilityCode { get; set; }
    public string? AbilityDescription { get; set; }
    public int? SubgroupId { get; set; }
    public string? SubgroupCode { get; set; }
    public string? SubgroupDescription { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryDescription { get; set; }
}
