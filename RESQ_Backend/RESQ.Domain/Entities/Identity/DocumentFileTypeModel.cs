namespace RESQ.Domain.Entities.Identity
{
    public class DocumentFileTypeModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? DocumentFileTypeCategoryId { get; set; }
        public DocumentFileTypeCategoryModel? DocumentFileTypeCategory { get; set; }
    }
}
