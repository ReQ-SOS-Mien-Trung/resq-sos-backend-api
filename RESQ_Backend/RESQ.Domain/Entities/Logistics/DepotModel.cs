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

    private readonly List<DepotManagerAssignment> _managerHistory = [];
    public IReadOnlyCollection<DepotManagerAssignment> ManagerHistory => _managerHistory.AsReadOnly();

    public Guid? CurrentManagerId => _managerHistory
        .Where(x => x.IsActive())
        .OrderByDescending(x => x.AssignedAt)
        .FirstOrDefault()?.UserId;

    // New property to access the full assignment object (including cached user details)
    // Returns the most recently assigned active manager
    public DepotManagerAssignment? CurrentManager => _managerHistory
        .Where(x => x.IsActive())
        .OrderByDescending(x => x.AssignedAt)
        .FirstOrDefault();
    
    // RESTORED: To support queries needing timestamp
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>Người cập nhật trạng thái kho gần nhất.</summary>
    public Guid? LastStatusChangedBy { get; set; }

    /// <summary>Người tạo kho.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Người cập nhật kho gần nhất.</summary>
    public Guid? LastUpdatedBy { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Hạn mức tối đa tổng tiền ứng trước cho kho này. 0 = không cho phép ứng.</summary>
    public decimal AdvanceLimit { get; set; }

    /// <summary>Tổng số tiền đã ứng trước và chưa hoàn trả.</summary>
    public decimal OutstandingAdvanceAmount { get; set; }

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
            CreatedBy = createdBy,
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

    public void UpdateDetails(string name, string address, GeoLocation location, decimal capacity, decimal weightCapacity, string? imageUrl = null, Guid? updatedBy = null)
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
        LastUpdatedBy = updatedBy;
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
    public void ChangeStatus(DepotStatus newStatus, Guid? changedBy = null)
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

        if (Status == DepotStatus.Unavailable && newStatus != DepotStatus.Available && newStatus != DepotStatus.Closing)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang ngưng hoạt động. Chỉ có thể chuyển về Available hoặc tiến hành đóng kho.");

        if (Status == DepotStatus.Closing && newStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho đang đóng kho. Chỉ có thể chuyển về Available hoặc đóng luôn.");

        // Transition matrix khớp với state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable, DepotStatus.Closing],
            [DepotStatus.Unavailable] = [DepotStatus.Available, DepotStatus.Closing],
            [DepotStatus.Closing] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuyển trạng thái từ '{Status.ToVietnamese()}' sang '{newStatus.ToVietnamese()}' không được phép.");

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
        LastStatusChangedBy = changedBy;
    }

    /// <summary>Admin cập nhật hạn mức ứng trước tối đa toàn kho. Không được thấp hơn lượng dư nợ hiện hành.</summary>
    public void SetAdvanceLimit(decimal limit)
    {
        if (limit < 0) throw new global::System.ArgumentOutOfRangeException(nameof(limit), "Hạn mức không được âm.");
        if (limit < OutstandingAdvanceAmount)
            throw new global::System.InvalidOperationException("Hạn mức ứng trước không hợp lệ.");
        AdvanceLimit = limit;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Ghi nhận khoản ứng trước vào kho. Cập nhật dư nợ.</summary>
    public void RecordAdvance(decimal amount)
    {
        if (amount <= 0) throw new global::System.ArgumentOutOfRangeException(nameof(amount), "Số tiền phải lớn hơn 0.");
        if (OutstandingAdvanceAmount + amount > AdvanceLimit)
            throw new global::System.InvalidOperationException("Vượt quá hạn mức ứng trước.");
        OutstandingAdvanceAmount += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Ghi nhận khoản hoàn trả ứng trước. Giảm dư nợ.</summary>
    public void RecordRepay(decimal amount)
    {
        if (amount <= 0) throw new global::System.ArgumentOutOfRangeException(nameof(amount), "Số tiền phải lớn hơn 0.");
        if (amount > OutstandingAdvanceAmount)
            throw new global::System.InvalidOperationException("Số tiền hoàn trả vượt quá dư nợ ứng trước.");
        OutstandingAdvanceAmount -= amount;
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
        CurrentUtilization = 0;
        CurrentWeightUtilization = 0;

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        activeAssignment?.Unassign(DateTime.UtcNow);

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

    /// <summary>
    /// Cập nhật mức sử dụng kho dựa trên thể tích và cân nặng.
    /// </summary>
    /// <param name="volumeAmount">Tổng thể tích cần thêm (dm³). Phải > 0.</param>
    /// <param name="weightAmount">Tổng cân nặng cần thêm (kg). Phải > 0.</param>
    public void UpdateUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable)
            throw new DepotClosingException("Kho đang ngưng hoạt động (Unavailable), không thể thực hiện thao tác này.");

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

    /// <summary>
    /// Giảm dung lượng sử dụng khi hàng xuất khỏi kho. Clamp về 0 nếu trừ vượt quá hiện tại.
    /// </summary>
    /// <param name="volumeAmount">Thể tích cần giảm (dm³). Phải >= 0.</param>
    /// <param name="weightAmount">Cân nặng cần giảm (kg). Phải >= 0.</param>
    public void DecreaseUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (volumeAmount < 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "thể tích");
        if (weightAmount < 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cân nặng");

        CurrentUtilization = Math.Max(0, CurrentUtilization - volumeAmount);
        CurrentWeightUtilization = Math.Max(0, CurrentWeightUtilization - weightAmount);
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gán thêm một manager mới cho kho. Không tự động gỡ manager cũ — đó là thao tác riêng biệt.
    /// Một manager có thể quản lý nhiều kho cùng lúc.
    /// </summary>
    public void AssignManager(Guid managerId)
    {
        if (managerId == Guid.Empty)
            throw new InvalidDepotManagerException();

        // Kiểm tra trùng: manager này đã đang active ở kho này chưa
        var alreadyActive = _managerHistory.Any(x => x.UserId == managerId && x.IsActive());
        if (alreadyActive)
            throw new DepotManagerAlreadyAssignedException(managerId);

        // Không gỡ manager cũ — gán mới là thao tác độc lập
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
