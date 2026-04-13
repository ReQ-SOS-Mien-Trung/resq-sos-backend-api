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
    /// True khi điểm tập kết đang có sự kiện triệu tập (Scheduled hoặc Gathering).
    /// Giá trị này được tính toán khi query, không lưu vào DB.
    /// </summary>
    public bool HasActiveEvent { get; set; }

    public AssemblyPointModel() { }

    /// <summary>
    /// Tạo điểm tập kết mới - trạng thái khởi đầu là <see cref="AssemblyPointStatus.Created"/>.
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
    /// Cập nhật thông tin điểm tập kết.
    /// Không được cập nhật khi đang <see cref="AssemblyPointStatus.Closed"/>.
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
    /// Chuyển trạng thái theo state-flow được phép:
    /// <list type="bullet">
    ///   <item>Created → Active</item>
    ///   <item>Active → Overloaded | Unavailable | Closed</item>
    ///   <item>Overloaded → Active | Unavailable (không thể Closed trực tiếp)</item>
    ///   <item>Unavailable → Active (Complete maintenance)</item>
    ///   <item>Closed → (không có chuyển đổi nào - viĩnh viễn)</item>
    /// </list>
    /// </summary>
    public void ChangeStatus(AssemblyPointStatus newStatus)
    {
        if (Status == newStatus) return;

        // Closed là trạng thái cuối - không thể thoát ra
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        var allowed = Status switch
        {
            AssemblyPointStatus.Created          => new[] { AssemblyPointStatus.Active },
            AssemblyPointStatus.Active           => new[] { AssemblyPointStatus.Overloaded, AssemblyPointStatus.Unavailable, AssemblyPointStatus.Closed },
            AssemblyPointStatus.Overloaded       => new[] { AssemblyPointStatus.Active, AssemblyPointStatus.Unavailable },
            // Theo state diagram: Unavailable chỉ có thể chuyển về Active (Complete maintenance)
            AssemblyPointStatus.Unavailable => new[] { AssemblyPointStatus.Active },
            _                                    => Array.Empty<AssemblyPointStatus>()
        };

        if (!allowed.Contains(newStatus))
            throw new InvalidAssemblyPointStatusTransitionException(Status, newStatus,
                $"Trạng thái cho phép từ {Status}: [{string.Join(", ", allowed)}].");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Kiểm tra sức chứa trước khi thêm <paramref name="additionalPersons"/> người vào điểm tập kết.
    /// Throws nếu điểm tập kết không trong trạng thái Active/Overloaded hoặc vượt sức chứa.
    /// Tự động chuyển sang <see cref="AssemblyPointStatus.Overloaded"/> khi đạt giới hạn.
    /// </summary>
    public void ValidatePersonCapacity(int currentPersonCount, int additionalPersons)
    {
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        if (Status is not (AssemblyPointStatus.Active or AssemblyPointStatus.Overloaded))
            throw new AssemblyPointUnavailableException();

        if (currentPersonCount + additionalPersons > MaxCapacity)
            throw new AssemblyPointCapacityExceededException(MaxCapacity, currentPersonCount, additionalPersons);

        // Tự động chuyển sang Overloaded khi đạt giới hạn
        if (currentPersonCount + additionalPersons == MaxCapacity && Status == AssemblyPointStatus.Active)
        {
            Status = AssemblyPointStatus.Overloaded;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Giải phóng sức chứa sau khi có người rời đi.
    /// Tự động chuyển <see cref="AssemblyPointStatus.Overloaded"/> → <see cref="AssemblyPointStatus.Active"/>
    /// khi còn chỗ trống.
    /// </summary>
    public void NotifyPersonRemoved(int remainingPersonCount)
    {
        if (Status == AssemblyPointStatus.Overloaded && remainingPersonCount < MaxCapacity)
        {
            Status = AssemblyPointStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

