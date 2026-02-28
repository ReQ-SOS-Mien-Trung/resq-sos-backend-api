using RESQ.Domain.Enum.Identity;

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
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? RescuerType { get; set; }
        public string? Address { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? Province { get; set; }

        // Documents
        public List<RescuerApplicationDocumentDto> Documents { get; set; } = new();
    }

    public class RescuerApplicationDocumentDto
    {
        public int Id { get; set; }
        public string? FileUrl { get; set; }
        public DocumentFileType FileType { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
