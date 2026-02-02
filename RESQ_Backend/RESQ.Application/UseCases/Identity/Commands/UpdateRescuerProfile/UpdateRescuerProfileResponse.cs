namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public class UpdateRescuerProfileResponse
    {
        public Guid UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsOnboarded { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Message { get; set; } = null!;
    }
}
