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
        int capacity,
        Guid? managerId = null,
        string? imageUrl = null)
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
            ImageUrl = imageUrl,
            LastUpdatedAt = DateTime.UtcNow
        };

        if (managerId.HasValue && managerId.Value != Guid.Empty)
        {
            depot.AssignManager(managerId.Value);
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, int capacity, string? imageUrl = null)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Closing)
            throw new DepotClosingException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity);

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException();

        Name = name;
        Address = address;
        Location = location;
        Capacity = capacity;
        if (imageUrl != null) ImageUrl = imageUrl;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Transition matrix theo state diagram:
    ///   Available → Full, UnderMaintenance
    ///   Full       → Available, UnderMaintenance
    ///   UnderMaintenance → Available
    /// PendingAssignment, Closing, Closed không đi qua phương thức này.
    /// </summary>
    public void ChangeStatus(DepotStatus newStatus)
    {
        if (Status == newStatus) return;

        // Trạng thái nguồn không thể thay đổi qua endpoint ChangeStatus
        if (Status == DepotStatus.PendingAssignment)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chưa có quản lý. Hãy chỉ định quản lý trước.");

        if (Status == DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang trong quá trình đóng. Hãy hoàn tất hoặc huỷ đóng kho trước.");

        if (Status == DepotStatus.Closed)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đã đóng vĩnh viễn, không thể thay đổi trạng thái.");

        // Transition matrix khớp với state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]        = [DepotStatus.Full, DepotStatus.UnderMaintenance],
            [DepotStatus.Full]             = [DepotStatus.Available, DepotStatus.UnderMaintenance],
            [DepotStatus.UnderMaintenance] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuyển trạng thái từ {Status} sang {newStatus} không được phép.");

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chưa có quản lý được chỉ định.");

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang vượt quá sức chứa.");

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // ── Depot Closure Methods ─────────────────────────────────────────

    /// <summary>
    /// Bước 1 đóng kho: đặt soft-lock Closing.
    /// Sau khi gọi method này, mọi thao tác xuất/nhập/điều chỉnh sẽ bị block.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closing)
            return; // Idempotent

        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status != DepotStatus.Available && Status != DepotStatus.Full)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closing,
                "Chỉ có thể đóng kho đang ở trạng thái Available hoặc Full.");

        Status = DepotStatus.Closing;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bước 2 đóng kho: hoàn tất đóng kho sau khi đã xử lý hàng tồn.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho phải ở trạng thái Closing trước khi đóng hoàn toàn.");

        Status = DepotStatus.Closed;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Khôi phục kho về trạng thái cũ khi huỷ hoặc timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Chỉ có thể khôi phục kho từ trạng thái Closing.");

        if (previousStatus != DepotStatus.Available && previousStatus != DepotStatus.Full)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Trạng thái khôi phục không hợp lệ.");

        Status = previousStatus;
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

        if (Status == DepotStatus.Closing)
            throw new DepotClosingException();

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

    /// <summary>
    /// Xoá manager đang active: set UnassignedAt (giữ lịch sử trong depot_managers).
    /// Chỉ cho phép khi kho ở trạng thái Available, Full hoặc UnderMaintenance.
    /// Sau khi xoá, status chuyển về PendingAssignment.
    /// </summary>
    public void DeleteManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Closing)
            throw new DepotClosingException(
                "Kho đang trong quá trình đóng, không thể xoá quản lý. Vui lòng huỷ đóng kho trước.");

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        if (activeAssignment == null)
            return; // Không có manager active — caller tự xử lý

        activeAssignment.Unassign(DateTime.UtcNow);

        Status = DepotStatus.PendingAssignment;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // ── Inventory lines (item-level stock, loaded from DepotSupplyInventory) ──
    private readonly List<DepotInventoryLine> _inventoryLines = [];
    public IReadOnlyList<DepotInventoryLine> InventoryLines => _inventoryLines.AsReadOnly();

    public void SetInventoryLines(IEnumerable<DepotInventoryLine> lines)
    {
        _inventoryLines.Clear();
        _inventoryLines.AddRange(lines);
    }
}

/// <summary>
/// Đại diện cho số lượng tồn kho khả dụng của một loại vật tư trong kho.
/// AvailableQuantity = Quantity - ReservedQuantity.
/// </summary>
public record DepotInventoryLine(
    int? ItemModelId,
    string ItemName,
    string? Unit,
    int AvailableQuantity
);
