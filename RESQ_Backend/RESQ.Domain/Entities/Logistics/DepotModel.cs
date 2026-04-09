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
            Status = DepotStatus.Created,
            ImageUrl = imageUrl,
            LastUpdatedAt = DateTime.UtcNow
        };

        if (managerId.HasValue && managerId.Value != Guid.Empty)
        {
            depot.AssignManager(managerId.Value);
            // Gán manager ngay lúc tạo → PendingAssignment (chưa hoạt động chính thức)
            depot.Status = DepotStatus.PendingAssignment;
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, int capacity, string? imageUrl = null)
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
        if (imageUrl != null) ImageUrl = imageUrl;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Transition matrix theo state diagram:
    ///   Available → UnderMaintenance, Unavailable
    ///   UnderMaintenance → Available
    ///   Unavailable → Available
    /// Created, PendingAssignment, Closed không đi qua phương thức này.
    /// Lưu ý: Không có trạng thái Full — hệ thống dùng CurrentUtilization vs Capacity để kiểm tra đầy kho.
    /// </summary>
    public void ChangeStatus(DepotStatus newStatus)
    {
        if (Status == newStatus) return;

        // Trạng thái nguồn không thể thay đổi qua endpoint ChangeStatus
        if (Status == DepotStatus.Created)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho vừa được tạo, chưa có quản lý. Hãy chỉ định quản lý trước.");

        if (Status == DepotStatus.PendingAssignment)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chưa có quản lý. Hãy chỉ định quản lý trước.");

        if (Status == DepotStatus.Closed)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đã đóng vĩnh viễn, không thể thay đổi trạng thái.");

        if (Status == DepotStatus.Unavailable && newStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang ngưng hoạt động. Chỉ có thể chuyển về Available hoặc tiến hành đóng kho.");

        // Transition matrix khớp với state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable],
            [DepotStatus.Unavailable] = [DepotStatus.Available],
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
    /// Bước 1 đóng kho: chuyển từ Unavailable → Closed.
    /// Admin phải set Unavailable trước, và kho phải trống (không còn hàng) mới được đóng.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho phải ở trạng thái Unavailable trước khi đóng. Hãy chuyển sang Unavailable trước.");

        // Không set Closing nữa — đi thẳng từ Unavailable.
        // Giữ phương thức để backward compat, CompleteClosing sẽ set Closed.
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bước 2 đóng kho: hoàn tất đóng kho sau khi đã xử lý hàng tồn.
    /// Kho phải ở trạng thái Unavailable.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho phải ở trạng thái Unavailable trước khi đóng hoàn toàn.");

        Status = DepotStatus.Closed;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Khôi phục kho về trạng thái cũ khi huỷ hoặc timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Chỉ có thể khôi phục kho từ trạng thái Unavailable.");

        if (previousStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Trạng thái khôi phục không hợp lệ. Chỉ có thể khôi phục về Available.");

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

        if (Status == DepotStatus.Unavailable)
            throw new DepotClosingException("Kho đang ngưng hoạt động (Unavailable), không thể thực hiện thao tác này.");

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

    /// <summary>
    /// Gỡ manager đang active (soft-unassign): set UnassignedAt, giữ lịch sử.
    /// Chỉ cho phép khi kho ở trạng thái Available.
    /// Sau khi gỡ, status chuyển về PendingAssignment.
    /// </summary>
    public void UnassignManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable)
            throw new DepotClosingException(
                "Kho đang ngưng hoạt động (Unavailable), không thể gỡ quản lý.");

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        activeAssignment?.Unassign(DateTime.UtcNow);

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
