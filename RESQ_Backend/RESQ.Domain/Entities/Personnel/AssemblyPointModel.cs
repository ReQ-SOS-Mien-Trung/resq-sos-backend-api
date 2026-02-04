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
    public int CapacityTeams { get; set; }
    public AssemblyPointStatus Status { get; set; }
    public GeoLocation? Location { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public AssemblyPointModel() { }

    public static AssemblyPointModel Create(
        string code,
        string name,
        int capacityTeams,
        GeoLocation location)
    {
        if (capacityTeams <= 0)
            throw new InvalidAssemblyPointCapacityException(capacityTeams);

        return new AssemblyPointModel
        {
            Code = code,
            Name = name,
            CapacityTeams = capacityTeams,
            Location = location,
            Status = AssemblyPointStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    public void UpdateDetails(string code, string name, int capacityTeams, GeoLocation location)
    {
        if (capacityTeams <= 0)
            throw new InvalidAssemblyPointCapacityException(capacityTeams);

        // Rule: Cannot update details if the Assembly Point is Unavailable
        // CHANGED: Generic DomainException -> AssemblyPointUnavailableException
        if (Status == AssemblyPointStatus.Unavailable)
        {
             throw new AssemblyPointUnavailableException();
        }

        Code = code;
        Name = name;
        CapacityTeams = capacityTeams;
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
}
