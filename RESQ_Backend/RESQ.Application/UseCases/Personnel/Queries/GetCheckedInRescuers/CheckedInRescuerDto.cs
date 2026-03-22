namespace RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;

public class CheckedInRescuerDto
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public DateTime CheckedInAt { get; set; }

    /// <summary>Rescuer đã được chia vào team (active) chưa.</summary>
    public bool IsInTeam { get; set; }

    /// <summary>Check-in trước giờ sự kiện (sớm).</summary>
    public bool IsEarly { get; set; }

    /// <summary>Check-in sau giờ sự kiện (muộn).</summary>
    public bool IsLate { get; set; }

    public List<string> TopAbilities { get; set; } = new();
}
