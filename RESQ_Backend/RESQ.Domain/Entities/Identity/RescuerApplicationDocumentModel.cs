namespace RESQ.Domain.Entities.Identity
{
    public class RescuerApplicationDocumentModel
    {
        public int Id { get; set; }
        public int? ApplicationId { get; set; }
        public string? FileUrl { get; set; }
        public int? FileTypeId { get; set; }
        public string? FileTypeCode { get; set; }
        public string? FileTypeName { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
