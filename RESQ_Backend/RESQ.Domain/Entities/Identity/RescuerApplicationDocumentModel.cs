namespace RESQ.Domain.Entities.Identity
{
    public class RescuerApplicationDocumentModel
    {
        public int Id { get; set; }
        public int? ApplicationId { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
