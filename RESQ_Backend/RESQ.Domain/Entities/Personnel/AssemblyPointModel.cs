    using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class AssemblyPointModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public AssemblyPointStatus Status { get; set; }
    public GeoLocation? Location { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>
    /// True khi di?m t?p k?t dang có s? ki?n tri?u t?p (Scheduled ho?c Gathering).
    /// Giá tr? này du?c tính toán khi query, không luu vào DB.
    /// </summary>
    public bool HasActiveEvent { get; set; }

    public AssemblyPointModel() { }

    /// <summary>
    /// T?o di?m t?p k?t m?i - tr?ng thái kh?i d?u là <see cref="AssemblyPointStatus.Created"/>.
    /// </summary>
    public static AssemblyPointModel Create(
        string code,
        string name,
        int maxCapacity,
        GeoLocation location,
        string? imageUrl = null)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        return new AssemblyPointModel
        {
            Code = code,
            Name = name,
            MaxCapacity = maxCapacity,
            Location = location,
            Status = AssemblyPointStatus.Created,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    /// <summary>
    /// C?p nh?t thông tin di?m t?p k?t.
    /// Không du?c c?p nh?t khi dang <see cref="AssemblyPointStatus.Closed"/>.
    /// </summary>
    public void UpdateDetails(string name, int maxCapacity, GeoLocation location, string? imageUrl = null)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        Name = name;
        MaxCapacity = maxCapacity;
        Location = location;
        if (imageUrl != null) ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Chuy?n tr?ng thái theo state-flow du?c phép:
    /// <list type="bullet">
    ///   <item>Created -> Active</item>
    ///   <item>Active -> Unavailable | Closed</item>
    ///   <item>Unavailable -> Active (Complete maintenance)</item>
    ///   <item>Closed -> (không có chuy?n d?i nào - vinh vi?n)</item>
    /// </list>
    /// </summary>
    public void ChangeStatus(AssemblyPointStatus newStatus)
    {
        if (Status == newStatus) return;

        // Closed là tr?ng thái cu?i - không th? thoát ra
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        var allowed = Status switch
        {
            AssemblyPointStatus.Created          => new[] { AssemblyPointStatus.Active },
            AssemblyPointStatus.Active           => new[] { AssemblyPointStatus.Unavailable, AssemblyPointStatus.Closed },
            // Theo state diagram: Unavailable ch? có th? chuy?n v? Active (Complete maintenance)
            AssemblyPointStatus.Unavailable => new[] { AssemblyPointStatus.Active },
            _                                    => Array.Empty<AssemblyPointStatus>()
        };

        if (!allowed.Contains(newStatus))
            throw new InvalidAssemblyPointStatusTransitionException(Status, newStatus,
                $"Tr?ng thái cho phép t? {Status}: [{string.Join(", ", allowed)}].");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Ki?m tra xem di?m t?p k?t có dang m? c?a d? nh?n thêm ngu?i không.
    /// Gi? dây không vang Exception n?u quá MaxCapacity (ch? tính toán t? l? ? DTO/UI).
    /// </summary>
    public void ValidatePersonCapacity(int currentPersonCount, int additionalPersons)
    {
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        if (Status != AssemblyPointStatus.Active)
            throw new AssemblyPointUnavailableException();
            
        // Removed hard block logic: no longer throw AssemblyPointCapacityExceededException
    }
}
