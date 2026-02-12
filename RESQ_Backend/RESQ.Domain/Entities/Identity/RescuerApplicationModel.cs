namespace RESQ.Domain.Entities.Identity
{
    public class RescuerApplicationModel
    {
        public int Id { get; set; }
        public Guid? UserId { get; set; }
        public string? Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedBy { get; set; }
        public string? AdminNote { get; set; }

        // Navigation properties
        public List<RescuerApplicationDocumentModel> Documents { get; set; } = new();
    }
}
