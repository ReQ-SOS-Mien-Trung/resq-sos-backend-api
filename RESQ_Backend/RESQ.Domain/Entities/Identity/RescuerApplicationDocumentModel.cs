using RESQ.Domain.Enum.Identity;

namespace RESQ.Domain.Entities.Identity
{
    public class RescuerApplicationDocumentModel
    {
        public int Id { get; set; }
        public int? ApplicationId { get; set; }
        public string? FileUrl { get; set; }
        public DocumentFileType FileType { get; set; } = DocumentFileType.OTHER;
        public DateTime? UploadedAt { get; set; }
    }
}
