using RESQ.Domain.Entities.Finance.Exceptions;
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

    /// <summary>S?c ch?a t?i da theo th? tích (dm).</summary>
    public decimal Capacity { get; set; }
    /// <summary>Th? tích hi?n t?i dang s? d?ng (dm).</summary>
    public decimal CurrentUtilization { get; set; }
    /// <summary>S?c ch?a t?i da theo cân n?ng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>Cân n?ng hi?n t?i dang s? d?ng (kg).</summary>
    public decimal CurrentWeightUtilization { get; set; }
    public DepotStatus Status { get; set; }

    public decimal AdvanceLimit { get; private set; }
    public decimal OutstandingAdvanceAmount { get; private set; }

    private readonly List<DepotManagerAssignment> _managerHistory = [];
    public IReadOnlyCollection<DepotManagerAssignment> ManagerHistory => _managerHistory.AsReadOnly();

    public Guid? CurrentManagerId => _managerHistory.FirstOrDefault(x => x.IsActive())?.UserId;
    
    // New property to access the full assignment object (including cached user details)
    public DepotManagerAssignment? CurrentManager => _managerHistory.FirstOrDefault(x => x.IsActive());
    
    // RESTORED: To support queries needing timestamp
    public DateTime? LastUpdatedAt { get; set; }

    public string? ImageUrl { get; set; }

    public DepotModel() { }

    public static DepotModel Create(
        string name,
        string address,
        GeoLocation location,
        decimal capacity,
        decimal weightCapacity,
        Guid? managerId = null,
        string? imageUrl = null)
    {
        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "th? tích");
        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cân n?ng");

        var depot = new DepotModel
        {
            Name = name,
            Address = address,
            Location = location,
            Capacity = capacity,
            CurrentUtilization = 0,
            WeightCapacity = weightCapacity,
            CurrentWeightUtilization = 0,
            Status = DepotStatus.Created,
            ImageUrl = imageUrl,
            LastUpdatedAt = DateTime.UtcNow
        };

        if (managerId.HasValue && managerId.Value != Guid.Empty)
        {
            depot.AssignManager(managerId.Value);
            // Gán manager ngay lúc t?o  PendingAssignment (chua ho?t d?ng chính th?c)
            depot.Status = DepotStatus.PendingAssignment;
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, decimal capacity, decimal weightCapacity, string? imageUrl = null)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "th? tích");

        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cân n?ng");

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException("S?c ch?a th? tích m?i th?p hon th? tích hàng hi?n t?i trong kho.");

        if (weightCapacity < CurrentWeightUtilization)
            throw new DepotCapacityExceededException("S?c ch?a cân n?ng m?i th?p hon cân n?ng hàng hi?n t?i trong kho.");

        Name = name;
        Address = address;
        Location = location;
        Capacity = capacity;
        WeightCapacity = weightCapacity;
        if (imageUrl != null) ImageUrl = imageUrl;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Transition matrix theo state diagram:
    ///   Available  UnderMaintenance, Unavailable
    ///   UnderMaintenance  Available
    ///   Unavailable  Available
    /// Created, PendingAssignment, Closed không di qua phuong th?c này.
    /// Luu ý: Không có tr?ng thái Full - h? th?ng dùng CurrentUtilization vs Capacity d? ki?m tra d?y kho.
    /// </summary>
    public void ChangeStatus(DepotStatus newStatus)
    {
        if (Status == newStatus) return;

        // Tr?ng thái ngu?n không th? thay d?i qua endpoint ChangeStatus
        if (Status == DepotStatus.Created)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho v?a du?c t?o, chua có qu?n lý. Hãy ch? d?nh qu?n lý tru?c.");

        if (Status == DepotStatus.PendingAssignment)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chua có qu?n lý. Hãy ch? d?nh qu?n lý tru?c.");

        if (Status == DepotStatus.Closed)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho dã dóng vinh vi?n, không th? thay d?i tr?ng thái.");

        if ((Status == DepotStatus.Unavailable || Status == DepotStatus.Closing) && newStatus != DepotStatus.Available && newStatus != DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang dóng kho";
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Kho {statusText}. Ch? có th? chuy?n v? Available ho?c ti?n hành dóng kho luôn.");
        }

        // Transition matrix kh?p v?i state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable, DepotStatus.Closing],
            [DepotStatus.Unavailable] = [DepotStatus.Available, DepotStatus.Closing], [DepotStatus.Closing] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuy?n tr?ng thái t? {Status} sang {newStatus} không du?c phép.");

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chua có qu?n lý du?c ch? d?nh.");

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho dang vu?t quá s?c ch?a th? tích.");

        if (newStatus == DepotStatus.Available && CurrentWeightUtilization > WeightCapacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho dang vu?t quá s?c ch?a cân n?ng.");

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // -- Depot Closure Methods -----------------------------------------

    /// <summary>
    /// Bu?c 1 dóng kho: chuy?n t? Unavailable  Closed.
    /// Admin ph?i set Closing tru?c, và kho ph?i tr?ng (không còn hàng) m?i du?c dóng.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status is not (DepotStatus.Closing or DepotStatus.Unavailable))
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho ph?i ? tr?ng thái Closing ho?c Unavailable tru?c khi dóng.");

        // Không set Closing n?a - di th?ng t? Unavailable.
        // Gi? phuong th?c d? backward compat, CompleteClosing s? set Closed.
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bu?c 2 dóng kho: hoàn t?t dóng kho sau khi dã x? lý hàng t?n.
    /// Kho ph?i ? tr?ng thái Closing.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho ph?i ? tr?ng thái Closing tru?c khi dóng hoàn toàn.");

        Status = DepotStatus.Closed;
        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        if (activeAssignment != null)
        {
            activeAssignment.Unassign(DateTime.UtcNow);
        }
        CurrentUtilization = 0;
        CurrentWeightUtilization = 0;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Khôi ph?c kho v? tr?ng thái cu khi hu? ho?c timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status is not (DepotStatus.Closing or DepotStatus.Unavailable))
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Ch? có th? khôi ph?c kho t? tr?ng thái Closing ho?c Unavailable.");

        if (previousStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Tr?ng thái khôi ph?c không h?p l?. Ch? có th? khôi ph?c v? Available.");

        Status = previousStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AddHistory(IEnumerable<DepotManagerAssignment> history)
    {
        _managerHistory.AddRange(history);
    }

    /// <summary>
    /// C?p nh?t m?c s? d?ng kho d?a trên th? tích và cân n?ng.
    /// </summary>
    /// <param name="volumeAmount">T?ng th? tích c?n thêm (dm). Ph?i > 0.</param>
    /// <param name="weightAmount">T?ng cân n?ng c?n thêm (kg). Ph?i > 0.</param>
    public void UpdateUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang dóng kho";
            throw new DepotClosingException($"Kho {statusText}, không th? th?c hi?n thao tác này.");
        }

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "th? tích");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cân n?ng");

        if (CurrentUtilization + volumeAmount > Capacity)
            throw new DepotCapacityExceededException("Th? tích kho không d? ch?a lu?ng hàng nh?p vào.");

        if (CurrentWeightUtilization + weightAmount > WeightCapacity)
            throw new DepotCapacityExceededException("Cân n?ng kho không d? ch?a lu?ng hàng nh?p vào.");

        CurrentUtilization += volumeAmount;
        CurrentWeightUtilization += weightAmount;
        LastUpdatedAt = DateTime.UtcNow;
    }
    public void DecreaseUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "th? tích");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cân n?ng");

        CurrentUtilization = Math.Max(0, CurrentUtilization - volumeAmount);
        CurrentWeightUtilization = Math.Max(0, CurrentWeightUtilization - weightAmount);
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

    /// <summary>
    /// G? manager dang active (soft-unassign): set UnassignedAt, gi? l?ch s?.
    /// Ch? cho phép khi kho ? tr?ng thái Available.
    /// Sau khi g?, status chuy?n v? PendingAssignment.
    /// </summary>
    public void UnassignManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang dóng kho";
            throw new DepotClosingException($"Kho {statusText}, không th? g? qu?n lý.");
        }

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        activeAssignment?.Unassign(DateTime.UtcNow);

        Status = DepotStatus.PendingAssignment;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // -- Inventory lines (item-level stock, loaded from DepotSupplyInventory) --
    private readonly List<DepotInventoryLine> _inventoryLines = [];
    public IReadOnlyList<DepotInventoryLine> InventoryLines => _inventoryLines.AsReadOnly();

    public void SetInventoryLines(IEnumerable<DepotInventoryLine> lines)
    {
        _inventoryLines.Clear();
        _inventoryLines.AddRange(lines);
    }

    public void SetAdvanceLimit(decimal limit)
    {
        if (limit < 0) throw new InvalidAdvanceLimitException(limit, OutstandingAdvanceAmount);
        if (limit < OutstandingAdvanceAmount) throw new InvalidAdvanceLimitException(limit, OutstandingAdvanceAmount);
        AdvanceLimit = limit;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void RecordAdvance(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        if (OutstandingAdvanceAmount + amount > AdvanceLimit) throw new AdvanceLimitExceededException(OutstandingAdvanceAmount, amount, AdvanceLimit);
        OutstandingAdvanceAmount += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void RecordRepay(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        if (OutstandingAdvanceAmount < amount) throw new OverRepaymentException(amount, OutstandingAdvanceAmount);
        OutstandingAdvanceAmount -= amount;
        LastUpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Ð?i di?n cho s? lu?ng t?n kho kh? d?ng c?a m?t lo?i v?t ph?m trong kho.
/// AvailableQuantity = Quantity - ReservedQuantity.
/// </summary>
public record DepotInventoryLine(
    int? ItemModelId,
    string ItemName,
    string? Unit,
    int AvailableQuantity
);


