using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public class GetRescuersItemResponse
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RescuerType { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsEligibleRescuer { get; set; }
    public int RescuerStep { get; set; }
    public bool IsBanned { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? BanReason { get; set; }
    public string? Address { get; set; }
    public string? Ward { get; set; }
    public string? Province { get; set; }
    public DateTime? CreatedAt { get; set; }
    public RescuerScoreDto? RescuerScore { get; set; }
}
