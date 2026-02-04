using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics;

public class DepotModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public GeoLocation? Location { get; set; }

    public int Capacity { get; set; }
    public int CurrentUtilization { get; set; }
    public DepotStatus Status { get; set; }

    private readonly List<DepotManagerAssignment> _managerHistory = [];
    public IReadOnlyCollection<DepotManagerAssignment> ManagerHistory => _managerHistory.AsReadOnly();

    public Guid? CurrentManagerId => _managerHistory.FirstOrDefault(x => x.IsActive())?.UserId;
    
    // RESTORED: To support queries needing timestamp
    public DateTime? LastUpdatedAt { get; set; }

    public DepotModel() { }

    public static DepotModel Create(
        string name,
        string address,
        GeoLocation location,
        int capacity,
        Guid? managerId = null)
    {
        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity);

        var depot = new DepotModel
        {
            Name = name,
            Address = address,
            Location = location,
            Capacity = capacity,
            CurrentUtilization = 0,
            Status = DepotStatus.PendingAssignment,
            LastUpdatedAt = DateTime.UtcNow
        };

        if (managerId.HasValue && managerId.Value != Guid.Empty)
        {
            depot.AssignManager(managerId.Value);
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, int capacity)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity);

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException();

        Name = name;
        Address = address;
        Location = location;
        Capacity = capacity;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(DepotStatus newStatus)
    {
        if (Status == newStatus) return;

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
        {
            throw new InvalidDepotStatusTransitionException(Status, newStatus, "Kho chưa có quản lý được chỉ định.");
        }

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
        {
             throw new InvalidDepotStatusTransitionException(Status, newStatus, "Kho đang vượt quá sức chứa.");
        }

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AddHistory(IEnumerable<DepotManagerAssignment> history)
    {
        _managerHistory.AddRange(history);
    }

    public void UpdateUtilization(int amount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (amount <= 0)
            throw new InvalidDepotUtilizationAmountException(amount);

        if (CurrentUtilization + amount > Capacity)
            throw new DepotCapacityExceededException();

        CurrentUtilization += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AssignManager(Guid managerId)
    {
        if (managerId == Guid.Empty)
            throw new InvalidDepotManagerException();

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        if (activeAssignment != null)
        {
            activeAssignment.Unassign(DateTime.UtcNow);
        }

        _managerHistory.Add(new DepotManagerAssignment(managerId, DateTime.UtcNow));
        
        Status = DepotStatus.Available;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UnassignManager()
    {
        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        activeAssignment?.Unassign(DateTime.UtcNow);

        Status = DepotStatus.PendingAssignment;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
