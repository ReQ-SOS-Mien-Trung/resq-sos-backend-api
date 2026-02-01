namespace RESQ.Domain.Entities.Resources.ValueObjects;

public class DepotManagerAssignment
{
    public Guid UserId { get; }
    public DateTime AssignedAt { get; }
    public DateTime? UnassignedAt { get; private set; }

    public DepotManagerAssignment(Guid userId, DateTime assignedAt, DateTime? unassignedAt = null)
    {
        UserId = userId;
        AssignedAt = assignedAt;
        UnassignedAt = unassignedAt;
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