namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public class DepotManagerAssignment
{
    public Guid UserId { get; }
    public DateTime AssignedAt { get; }
    public DateTime? UnassignedAt { get; private set; }

    // Cached details for read-models
    public string? FullName { get; }
    public string? Email { get; }
    public string? Phone { get; }

    public DepotManagerAssignment(
        Guid userId, 
        DateTime assignedAt, 
        DateTime? unassignedAt = null,
        string? fullName = null,
        string? email = null,
        string? phone = null)
    {
        UserId = userId;
        AssignedAt = assignedAt;
        UnassignedAt = unassignedAt;
        FullName = fullName;
        Email = email;
        Phone = phone;
    }

    public void Unassign(DateTime unassignedAt)
    {
        UnassignedAt = unassignedAt;
    }

    public bool IsActive()
    {
        return UnassignedAt == null;
    }
}
