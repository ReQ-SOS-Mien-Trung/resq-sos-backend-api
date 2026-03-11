namespace RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;

public class AdminCreateUserRequestDto
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string Password { get; set; } = null!;
    public int RoleId { get; set; }
    public string? RescuerType { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? Ward { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsEmailVerified { get; set; } = false;
    public bool IsOnboarded { get; set; } = false;
    public bool IsEligibleRescuer { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
