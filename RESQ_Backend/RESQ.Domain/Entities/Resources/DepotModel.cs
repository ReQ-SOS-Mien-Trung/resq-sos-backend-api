using RESQ.Domain.Entities.Resources.Exceptions;
using RESQ.Domain.Entities.Resources.ValueObjects;
using RESQ.Domain.Enum.Resources;

namespace RESQ.Domain.Entities.Resources;

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

    // Computed property to get the active manager ID
    public Guid? CurrentManagerId => _managerHistory.FirstOrDefault(x => x.IsActive())?.UserId;
    
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

        // Close any existing active assignment
        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        if (activeAssignment != null)
        {
            // If re-assigning same manager, do nothing or update logic as needed. 
            // Here we assume a new assignment cycle.
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
