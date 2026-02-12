namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications
{
    public class RescuerApplicationDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string? Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedBy { get; set; }
        public string? AdminNote { get; set; }

        // User info
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? RescuerType { get; set; }
        public string? Address { get; set; }
        public string? Ward { get; set; }
        public string? City { get; set; }

        // Documents
        public List<RescuerApplicationDocumentDto> Documents { get; set; } = new();
    }

    public class RescuerApplicationDocumentDto
    {
        public int Id { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
