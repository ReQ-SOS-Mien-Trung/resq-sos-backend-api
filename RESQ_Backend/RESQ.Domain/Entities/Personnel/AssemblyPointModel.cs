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
    ///   <item>Created -> Active</item>
    ///   <item>Active -> Unavailable | Closed</item>
    ///   <item>Unavailable -> Active (Complete maintenance)</item>
    ///   <item>Closed -> (không có chuyển đổi nào - vĩnh viễn)</item>
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
            AssemblyPointStatus.Active           => new[] { AssemblyPointStatus.Unavailable, AssemblyPointStatus.Closed },
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
    /// Kiểm tra xem điểm tập kết có đang mở cửa để nhận thêm người không.
    /// Giờ đây không văng Exception nếu quá MaxCapacity (chỉ tính toán tỷ lệ ở DTO/UI).
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
