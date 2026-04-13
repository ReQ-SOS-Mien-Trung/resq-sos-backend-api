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

    /// <summary>Sức chứa tối đa theo thể tích (dm³).</summary>
    public decimal Capacity { get; set; }
    /// <summary>Thể tích hiện tại đang sử dụng (dm³).</summary>
    public decimal CurrentUtilization { get; set; }
    /// <summary>Sức chứa tối đa theo cân nặng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>Cân nặng hiện tại đang sử dụng (kg).</summary>
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
            throw new InvalidDepotCapacityException(capacity, "thể tích");
        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cân nặng");

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
            // Gán manager ngay lúc tạo → PendingAssignment (chưa hoạt động chính thức)
            depot.Status = DepotStatus.PendingAssignment;
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, decimal capacity, decimal weightCapacity, string? imageUrl = null)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "thể tích");

        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cân nặng");

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException("Sức chứa thể tích mới thấp hơn thể tích hàng hiện tại trong kho.");

        if (weightCapacity < CurrentWeightUtilization)
            throw new DepotCapacityExceededException("Sức chứa cân nặng mới thấp hơn cân nặng hàng hiện tại trong kho.");

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
    ///   Available → UnderMaintenance, Unavailable
    ///   UnderMaintenance → Available
    ///   Unavailable → Available
    /// Created, PendingAssignment, Closed không đi qua phương thức này.
    /// Lưu ý: Không có trạng thái Full - hệ thống dùng CurrentUtilization vs Capacity để kiểm tra đầy kho.
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

        if ((Status == DepotStatus.Unavailable || Status == DepotStatus.Closing) && newStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang ngưng hoạt động/đóng kho. Chỉ có thể chuyển về Available hoặc đóng luôn.");

        // Transition matrix khớp với state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable, DepotStatus.Closing],
            [DepotStatus.Unavailable] = [DepotStatus.Available], [DepotStatus.Closing] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuyển trạng thái từ {Status} sang {newStatus} không được phép.");

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chưa có quản lý được chỉ định.");

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang vượt quá sức chứa thể tích.");

        if (newStatus == DepotStatus.Available && CurrentWeightUtilization > WeightCapacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang vượt quá sức chứa cân nặng.");

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // -- Depot Closure Methods -----------------------------------------

    /// <summary>
    /// Bước 1 đóng kho: chuyển từ Unavailable → Closed.
    /// Admin phải set Closing trước, và kho phải trống (không còn hàng) mới được đóng.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho phải ở trạng thái Closing trước khi đóng. Hãy chuyển sang Closing trước.");

        // Không set Closing nữa - đi thẳng từ Unavailable.
        // Giữ phương thức để backward compat, CompleteClosing sẽ set Closed.
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bước 2 đóng kho: hoàn tất đóng kho sau khi đã xử lý hàng tồn.
    /// Kho phải ở trạng thái Closing.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho phải ở trạng thái Closing.trước khi đóng hoàn toàn.");

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
    /// Khôi phục kho về trạng thái cũ khi huỷ hoặc timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Chỉ có thể Khôi phục kho từ trạng thái Closing.");

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

    /// <summary>
    /// Cập nhật mức sử dụng kho dựa trên thể tích và cân nặng.
    /// </summary>
    /// <param name="volumeAmount">Tổng thể tích cần thêm (dm³). Phải > 0.</param>
    /// <param name="weightAmount">Tổng cân nặng cần thêm (kg). Phải > 0.</param>
    public void UpdateUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
            throw new DepotClosingException("Kho đang ngưng hoạt động/đóng kho, không thể thực hiện thao tác này.");

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "thể tích");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cân nặng");

        if (CurrentUtilization + volumeAmount > Capacity)
            throw new DepotCapacityExceededException("Thể tích kho không đủ chứa lượng hàng nhập vào.");

        if (CurrentWeightUtilization + weightAmount > WeightCapacity)
            throw new DepotCapacityExceededException("Cân nặng kho không đủ chứa lượng hàng nhập vào.");

        CurrentUtilization += volumeAmount;
        CurrentWeightUtilization += weightAmount;
        LastUpdatedAt = DateTime.UtcNow;
    }
    public void DecreaseUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "thể tích");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cân nặng");

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
    /// Gỡ manager đang active (soft-unassign): set UnassignedAt, giữ lịch sử.
    /// Chỉ cho phép khi kho ở trạng thái Available.
    /// Sau khi gỡ, status chuyển về PendingAssignment.
    /// </summary>
    public void UnassignManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable || Status == DepotStatus.Closing)
            throw new DepotClosingException(
                "Kho đang ngưng hoạt động/đóng kho, không thể gỡ quản lý.");

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
/// Đại diện cho số lượng tồn kho khả dụng của một loại vật tư trong kho.
/// AvailableQuantity = Quantity - ReservedQuantity.
/// </summary>
public record DepotInventoryLine(
    int? ItemModelId,
    string ItemName,
    string? Unit,
    int AvailableQuantity
);







