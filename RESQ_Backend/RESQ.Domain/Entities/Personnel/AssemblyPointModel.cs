using RESQ.Domain.Entities.Exceptions;
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

    public static AssemblyPointModel Create(
        string code,
        string name,
        int maxCapacity,
        GeoLocation location)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        return new AssemblyPointModel
        {
            Code = code,
            Name = name,
            MaxCapacity = maxCapacity,
            Location = location,
            Status = AssemblyPointStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    public void UpdateDetails(string code, string name, int maxCapacity, GeoLocation location)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        // Rule: Cannot update details if the Assembly Point is Unavailable
        // CHANGED: Generic DomainException -> AssemblyPointUnavailableException
        if (Status == AssemblyPointStatus.Unavailable)
        {
             throw new AssemblyPointUnavailableException();
        }

        Code = code;
        Name = name;
        MaxCapacity = maxCapacity;
        Location = location;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(AssemblyPointStatus newStatus)
    {
        if (Status == newStatus) return;

        // Rule 1: Cannot switch to 'Overloaded' if currently 'Unavailable'
        if (Status == AssemblyPointStatus.Unavailable && newStatus == AssemblyPointStatus.Overloaded)
        {
            throw new InvalidAssemblyPointStatusTransitionException(
                Status, 
                newStatus, 
                "Điểm tập kết phải được kích hoạt (Active) trước khi báo quá tải.");
        }

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Kiểm tra năng lực trước khi gán thêm <paramref name="additionalTeams"/> đội vào điểm tập kết.
    /// Throws nếu điểm tập kết không hoạt động hoặc vượt sức chứa.
    /// </summary>
    public void ValidateTeamAssignment(int currentTeamCount, int additionalTeams)
    {
        if (Status == AssemblyPointStatus.Unavailable)
            throw new AssemblyPointUnavailableException();

        if (currentTeamCount + additionalTeams > MaxCapacity)
            throw new AssemblyPointCapacityExceededException(MaxCapacity, currentTeamCount, additionalTeams);
    }
}
