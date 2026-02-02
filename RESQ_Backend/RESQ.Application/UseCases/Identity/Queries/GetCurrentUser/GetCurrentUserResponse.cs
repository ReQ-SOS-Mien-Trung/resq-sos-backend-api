namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public class GetCurrentUserResponse
    {
        public Guid Id { get; set; }
        public int? RoleId { get; set; }
        public string? FullName { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? RescuerType { get; set; }
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsOnboarded { get; set; }
        public bool IsEligibleRescuer { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
