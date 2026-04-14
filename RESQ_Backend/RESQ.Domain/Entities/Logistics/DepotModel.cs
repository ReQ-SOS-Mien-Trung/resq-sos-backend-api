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

    /// <summary>S?c ch?a t?i da theo th? t�ch (dm).</summary>
    public decimal Capacity { get; set; }
    /// <summary>Th? t�ch hi?n t?i dang s? d?ng (dm).</summary>
    public decimal CurrentUtilization { get; set; }
    /// <summary>S?c ch?a t?i da theo c�n n?ng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>C�n n?ng hi?n t?i dang s? d?ng (kg).</summary>
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


    /// <summary>Ngu?i c?p nh?t tr?ng th�i kho g?n nh?t.</summary>
    public Guid? LastStatusChangedBy { get; set; }
    /// <summary>Người tạo kho.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Người cập nhật kho gần nhất.</summary>
    public Guid? LastUpdatedBy { get; set; }

    public string? ImageUrl { get; set; }

    public DepotModel() { }

    public static DepotModel Create(
        string name,
        string address,
        GeoLocation location,
        decimal capacity,
        decimal weightCapacity,
        Guid? managerId = null,
        string? imageUrl = null,
        Guid? createdBy = null)
    {
        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "th? t�ch");
        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "c�n n?ng");

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
            CreatedBy = createdBy,
            LastUpdatedAt = DateTime.UtcNow
        };

        if (managerId.HasValue && managerId.Value != Guid.Empty)
        {
            depot.AssignManager(managerId.Value);
            // G�n manager ngay l�c t?o  PendingAssignment (chua ho?t d?ng ch�nh th?c)
            depot.Status = DepotStatus.PendingAssignment;
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, decimal capacity, decimal weightCapacity, string? imageUrl = null, Guid? updatedBy = null)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "th? t�ch");

        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "c�n n?ng");

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException("S?c ch?a th? t�ch m?i th?p hon th? t�ch h�ng hi?n t?i trong kho.");

        if (weightCapacity < CurrentWeightUtilization)
            throw new DepotCapacityExceededException("S?c ch?a c�n n?ng m?i th?p hon c�n n?ng h�ng hi?n t?i trong kho.");

        Name = name;
        Address = address;
        Location = location;
        Capacity = capacity;
        WeightCapacity = weightCapacity;
        if (imageUrl != null) ImageUrl = imageUrl;
        LastUpdatedBy = updatedBy;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Transition matrix theo state diagram:
    ///   Available  UnderMaintenance, Unavailable
    ///   UnderMaintenance  Available
    ///   Unavailable  Available
    /// Created, PendingAssignment, Closed kh�ng di qua phuong th?c n�y.
    /// Luu �: Kh�ng c� tr?ng th�i Full - h? th?ng d�ng CurrentUtilization vs Capacity d? ki?m tra d?y kho.
    /// </summary>
    public void ChangeStatus(DepotStatus newStatus, Guid? changedBy = null)
    {
        if (Status == newStatus) return;

        // Tr?ng th�i ngu?n kh�ng th? thay d?i qua endpoint ChangeStatus
        if (Status == DepotStatus.Created)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho v?a du?c t?o, chua c� qu?n l�. H�y ch? d?nh qu?n l� tru?c.");

        if (Status == DepotStatus.PendingAssignment)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chua c� qu?n l�. H�y ch? d?nh qu?n l� tru?c.");

        if (Status == DepotStatus.Closed)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho d� d�ng vinh vi?n, kh�ng th? thay d?i tr?ng th�i.");

        if ((Status == DepotStatus.Unavailable || Status == DepotStatus.Closing) && newStatus != DepotStatus.Available && newStatus != DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang d�ng kho";
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Kho {statusText}. Ch? c� th? chuy?n v? Available ho?c ti?n h�nh d�ng kho lu�n.");
        }

        // Transition matrix kh?p v?i state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable, DepotStatus.Closing],
            [DepotStatus.Unavailable] = [DepotStatus.Available, DepotStatus.Closing], [DepotStatus.Closing] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuy?n tr?ng th�i t? {Status} sang {newStatus} kh�ng du?c ph�p.");

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chua c� qu?n l� du?c ch? d?nh.");

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho dang vu?t qu� s?c ch?a th? t�ch.");

        if (newStatus == DepotStatus.Available && CurrentWeightUtilization > WeightCapacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho dang vu?t qu� s?c ch?a c�n n?ng.");

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
        LastStatusChangedBy = changedBy;
    }

    // -- Depot Closure Methods -----------------------------------------

    /// <summary>
    /// Bu?c 1 d�ng kho: chuy?n t? Unavailable  Closed.
    /// Admin ph?i set Closing tru?c, v� kho ph?i tr?ng (kh�ng c�n h�ng) m?i du?c d�ng.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status is not (DepotStatus.Closing or DepotStatus.Unavailable))
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho ph?i ? tr?ng th�i Closing ho?c Unavailable tru?c khi d�ng.");

        // Kh�ng set Closing n?a - di th?ng t? Unavailable.
        // Gi? phuong th?c d? backward compat, CompleteClosing s? set Closed.
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bu?c 2 d�ng kho: ho�n t?t d�ng kho sau khi d� x? l� h�ng t?n.
    /// Kho ph?i ? tr?ng th�i Closing.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho ph?i ? tr?ng th�i Closing tru?c khi d�ng ho�n to�n.");

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
    /// Kh�i ph?c kho v? tr?ng th�i cu khi hu? ho?c timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status is not (DepotStatus.Closing or DepotStatus.Unavailable))
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Ch? c� th? kh�i ph?c kho t? tr?ng th�i Closing ho?c Unavailable.");

        if (previousStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Tr?ng th�i kh�i ph?c kh�ng h?p l?. Ch? c� th? kh�i ph?c v? Available.");

        Status = previousStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AddHistory(IEnumerable<DepotManagerAssignment> history)
    {
        _managerHistory.AddRange(history);
    }

    /// <summary>
    /// C?p nh?t m?c s? d?ng kho d?a tr�n th? t�ch v� c�n n?ng.
    /// </summary>
    /// <param name="volumeAmount">T?ng th? t�ch c?n th�m (dm). Ph?i > 0.</param>
    /// <param name="weightAmount">T?ng c�n n?ng c?n th�m (kg). Ph?i > 0.</param>
    public void UpdateUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang d�ng kho";
            throw new DepotClosingException($"Kho {statusText}, kh�ng th? th?c hi?n thao t�c n�y.");
        }

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "th? t�ch");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "c�n n?ng");

        if (CurrentUtilization + volumeAmount > Capacity)
            throw new DepotCapacityExceededException("Th? t�ch kho kh�ng d? ch?a lu?ng h�ng nh?p v�o.");

        if (CurrentWeightUtilization + weightAmount > WeightCapacity)
            throw new DepotCapacityExceededException("C�n n?ng kho kh�ng d? ch?a lu?ng h�ng nh?p v�o.");

        CurrentUtilization += volumeAmount;
        CurrentWeightUtilization += weightAmount;
        LastUpdatedAt = DateTime.UtcNow;
    }
    public void DecreaseUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "th? t�ch");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "c�n n?ng");

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
    /// Ch? cho ph�p khi kho ? tr?ng th�i Available.
    /// Sau khi g?, status chuy?n v? PendingAssignment.
    /// </summary>
    public void UnassignManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
        {
            string statusText = Status == DepotStatus.Unavailable ? "dang ngung ho?t d?ng" : "dang d�ng kho";
            throw new DepotClosingException($"Kho {statusText}, kh�ng th? g? qu?n l�.");
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
/// �?i di?n cho s? lu?ng t?n kho kh? d?ng c?a m?t lo?i v?t ph?m trong kho.
/// AvailableQuantity = Quantity - ReservedQuantity.
/// </summary>
public record DepotInventoryLine(
    int? ItemModelId,
    string ItemName,
    string? Unit,
    int AvailableQuantity
);


