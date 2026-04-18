namespace RESQ.Domain.Entities.Identity;

public class DocumentFileTypeCategoryModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public static DocumentFileTypeCategoryModel Create(string code, string? description) => new()
    {
        Code = code,
        Description = description
    };

    public void Update(string code, string? description)
    {
        Code = code;
        Description = description;
    }
}
