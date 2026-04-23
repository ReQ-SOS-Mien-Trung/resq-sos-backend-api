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

    /// <summary>Lý do chuyển trạng thái gần nhất (bắt buộc khi Closed, tuỳ chọn khi Unavailable).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Thời điểm chuyển trạng thái gần nhất (UTC).</summary>
    public DateTime? StatusChangedAt { get; set; }

    /// <summary>UserId của người thực hiện chuyển trạng thái gần nhất.</summary>
    public Guid? StatusChangedBy { get; set; }

    /// <summary>
    /// True khi điểm tập kết đang có sự kiện triệu tập đang hoạt động.
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
    /// <list type="bullet">
    ///   <item>Không được cập nhật khi <see cref="AssemblyPointStatus.Closed"/>.</item>
    ///   <item>Khi <see cref="AssemblyPointStatus.Available"/>: chỉ cho phép <b>tăng</b> MaxCapacity;
    ///         Name, Location, ImageUrl không được thay đổi.</item>
    ///   <item>Khi <see cref="AssemblyPointStatus.Created"/> hoặc <see cref="AssemblyPointStatus.Unavailable"/>:
    ///         cho phép cập nhật tất cả.</item>
    /// </list>
    /// </summary>
    public void UpdateDetails(string name, int maxCapacity, GeoLocation location, string? imageUrl = null)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        if (Status == AssemblyPointStatus.Available)
        {
            // Chỉ cho phép tăng MaxCapacity khi đang vận hành
            if (maxCapacity < MaxCapacity)
                throw new BadAssemblyPointUpdateException(
                    "Không thể giảm sức chứa khi điểm tập kết đang hoạt động. " +
                    $"Sức chứa hiện tại: {MaxCapacity}. Chỉ cho phép tăng.");

            MaxCapacity = maxCapacity;
            UpdatedAt = DateTime.UtcNow;
            return;
        }

        // Created hoặc Unavailable: cập nhật tất cả
        Name = name;
        MaxCapacity = maxCapacity;
        Location = location;
        if (imageUrl != null) ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Chuyển trạng thái theo state-flow được phép:
    /// <list type="bullet">
    ///   <item>Created → Available | Closed</item>
    ///   <item>Available ⇄ Unavailable (hai chiều)</item>
    ///   <item>Unavailable → Closed</item>
    ///   <item>Closed → (không có chuyển đổi nào - vĩnh viễn)</item>
    /// </list>
    /// </summary>
    /// <param name="newStatus">Trạng thái mới.</param>
    /// <param name="changedBy">UserId của người thực hiện (để audit).</param>
    /// <param name="reason">Lý do (bắt buộc khi Closed, tuỳ chọn khi Unavailable).</param>
    public void ChangeStatus(AssemblyPointStatus newStatus, Guid changedBy, string? reason = null)
    {
        if (Status == newStatus) return;

        // Closed là trạng thái cuối - không thể thoát ra
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        var allowed = Status switch
        {
            AssemblyPointStatus.Created     => new[] { AssemblyPointStatus.Available, AssemblyPointStatus.Closed },
            AssemblyPointStatus.Available   => new[] { AssemblyPointStatus.Unavailable, AssemblyPointStatus.Closed },
            AssemblyPointStatus.Unavailable => new[] { AssemblyPointStatus.Available, AssemblyPointStatus.Closed },
            _                               => Array.Empty<AssemblyPointStatus>()
        };

        if (!allowed.Contains(newStatus))
            throw new InvalidAssemblyPointStatusTransitionException(Status, newStatus,
                $"Trạng thái cho phép từ {Status}: [{string.Join(", ", allowed)}].");

        if (newStatus == AssemblyPointStatus.Closed && string.IsNullOrWhiteSpace(reason))
            throw new BadAssemblyPointUpdateException(
                "Bắt buộc phải nhập lý do khi đóng điểm tập kết vĩnh viễn.");

        Status = newStatus;
        StatusReason = reason;
        StatusChangedAt = DateTime.UtcNow;
        StatusChangedBy = changedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Kiểm tra điểm tập kết có thể nhận thêm người không.
    /// Hard-limit: ném <see cref="AssemblyPointCapacityExceededException"/> nếu vượt MaxCapacity.
    /// </summary>
    public void ValidatePersonCapacity(int currentPersonCount, int additionalPersons)
    {
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        if (Status != AssemblyPointStatus.Available)
            throw new AssemblyPointUnavailableException();

        if (currentPersonCount + additionalPersons > MaxCapacity)
            throw new AssemblyPointCapacityExceededException(MaxCapacity, currentPersonCount, additionalPersons);
    }
}
