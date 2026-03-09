namespace RESQ.Application.UseCases.Identity.Queries.GetUsers;

public class GetUsersItemResponse
{
    public Guid Id { get; set; }
    public int? RoleId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RescuerType { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsOnboarded { get; set; }
    public bool IsEligibleRescuer { get; set; }
    public bool IsBanned { get; set; }
    public Guid? BannedBy { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? BanReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
