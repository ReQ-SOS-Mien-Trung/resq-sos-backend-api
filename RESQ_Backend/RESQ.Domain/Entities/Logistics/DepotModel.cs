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

    /// <summary>Sá»©c chá»©a tá»‘i Ä‘a theo thá»ƒ tÃ­ch (dmÂ³).</summary>
    public decimal Capacity { get; set; }
    /// <summary>Thá»ƒ tÃ­ch hiá»‡n táº¡i Ä‘ang sá»­ dá»¥ng (dmÂ³).</summary>
    public decimal CurrentUtilization { get; set; }
    /// <summary>Sá»©c chá»©a tá»‘i Ä‘a theo cÃ¢n náº·ng (kg).</summary>
    public decimal WeightCapacity { get; set; }
    /// <summary>CÃ¢n náº·ng hiá»‡n táº¡i Ä‘ang sá»­ dá»¥ng (kg).</summary>
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
            throw new InvalidDepotCapacityException(capacity, "thá»ƒ tÃ­ch");
        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cÃ¢n náº·ng");

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
            // GÃ¡n manager ngay lÃºc táº¡o â†’ PendingAssignment (chÆ°a hoáº¡t Ä‘á»™ng chÃ­nh thá»©c)
            depot.Status = DepotStatus.PendingAssignment;
        }

        return depot;
    }

    public void UpdateDetails(string name, string address, GeoLocation location, decimal capacity, decimal weightCapacity, string? imageUrl = null)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity, "thá»ƒ tÃ­ch");

        if (weightCapacity <= 0)
            throw new InvalidDepotCapacityException(weightCapacity, "cÃ¢n náº·ng");

        if (capacity < CurrentUtilization)
            throw new DepotCapacityExceededException("Sá»©c chá»©a thá»ƒ tÃ­ch má»›i tháº¥p hÆ¡n thá»ƒ tÃ­ch hÃ ng hiá»‡n táº¡i trong kho.");

        if (weightCapacity < CurrentWeightUtilization)
            throw new DepotCapacityExceededException("Sá»©c chá»©a cÃ¢n náº·ng má»›i tháº¥p hÆ¡n cÃ¢n náº·ng hÃ ng hiá»‡n táº¡i trong kho.");

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
    ///   Available â†’ UnderMaintenance, Unavailable
    ///   UnderMaintenance â†’ Available
    ///   Unavailable â†’ Available
    /// Created, PendingAssignment, Closed khÃ´ng Ä‘i qua phÆ°Æ¡ng thá»©c nÃ y.
    /// LÆ°u Ã½: KhÃ´ng cÃ³ tráº¡ng thÃ¡i Full â€” há»‡ thá»‘ng dÃ¹ng CurrentUtilization vs Capacity Ä‘á»ƒ kiá»ƒm tra Ä‘áº§y kho.
    /// </summary>
    public void ChangeStatus(DepotStatus newStatus)
    {
        if (Status == newStatus) return;

        // Tráº¡ng thÃ¡i nguá»“n khÃ´ng thá»ƒ thay Ä‘á»•i qua endpoint ChangeStatus
        if (Status == DepotStatus.Created)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho vá»«a Ä‘Æ°á»£c táº¡o, chÆ°a cÃ³ quáº£n lÃ½. HÃ£y chá»‰ Ä‘á»‹nh quáº£n lÃ½ trÆ°á»›c.");

        if (Status == DepotStatus.PendingAssignment)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chÆ°a cÃ³ quáº£n lÃ½. HÃ£y chá»‰ Ä‘á»‹nh quáº£n lÃ½ trÆ°á»›c.");

        if (Status == DepotStatus.Closed)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho Ä‘Ã£ Ä‘Ã³ng vÄ©nh viá»…n, khÃ´ng thá»ƒ thay Ä‘á»•i tráº¡ng thÃ¡i.");

        if (Status == DepotStatus.Unavailable && newStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho Ä‘ang ngÆ°ng hoáº¡t Ä‘á»™ng. Chá»‰ cÃ³ thá»ƒ chuyá»ƒn vá» Available hoáº·c tiáº¿n hÃ nh Ä‘Ã³ng kho.");

        // Transition matrix khá»›p vá»›i state diagram
        var allowed = new Dictionary<DepotStatus, HashSet<DepotStatus>>
        {
            [DepotStatus.Available]   = [DepotStatus.Unavailable],
            [DepotStatus.Unavailable] = [DepotStatus.Available],
        };

        if (!allowed.TryGetValue(Status, out var validTargets) || !validTargets.Contains(newStatus))
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                $"Chuyá»ƒn tráº¡ng thÃ¡i tá»« {Status} sang {newStatus} khÃ´ng Ä‘Æ°á»£c phÃ©p.");

        if (newStatus == DepotStatus.Available && CurrentManagerId == null)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho chÆ°a cÃ³ quáº£n lÃ½ Ä‘Æ°á»£c chá»‰ Ä‘á»‹nh.");

        if (newStatus == DepotStatus.Available && CurrentUtilization > Capacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho Ä‘ang vÆ°á»£t quÃ¡ sá»©c chá»©a thá»ƒ tÃ­ch.");

        if (newStatus == DepotStatus.Available && CurrentWeightUtilization > WeightCapacity)
            throw new InvalidDepotStatusTransitionException(Status, newStatus,
                "Kho Ä‘ang vÆ°á»£t quÃ¡ sá»©c chá»©a cÃ¢n náº·ng.");

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // â”€â”€ Depot Closure Methods â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// BÆ°á»›c 1 Ä‘Ã³ng kho: chuyá»ƒn tá»« Unavailable â†’ Closed.
    /// Admin pháº£i set Unavailable trÆ°á»›c, vÃ  kho pháº£i trá»‘ng (khÃ´ng cÃ²n hÃ ng) má»›i Ä‘Æ°á»£c Ä‘Ã³ng.
    /// </summary>
    public void InitiateClosing()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho pháº£i á»Ÿ tráº¡ng thÃ¡i Unavailable trÆ°á»›c khi Ä‘Ã³ng. HÃ£y chuyá»ƒn sang Unavailable trÆ°á»›c.");

        // KhÃ´ng set Closing ná»¯a â€” Ä‘i tháº³ng tá»« Unavailable.
        // Giá»¯ phÆ°Æ¡ng thá»©c Ä‘á»ƒ backward compat, CompleteClosing sáº½ set Closed.
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// BÆ°á»›c 2 Ä‘Ã³ng kho: hoÃ n táº¥t Ä‘Ã³ng kho sau khi Ä‘Ã£ xá»­ lÃ½ hÃ ng tá»“n.
    /// Kho pháº£i á»Ÿ tráº¡ng thÃ¡i Unavailable.
    /// </summary>
    public void CompleteClosing()
    {
        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, DepotStatus.Closed,
                "Kho pháº£i á»Ÿ tráº¡ng thÃ¡i Unavailable trÆ°á»›c khi Ä‘Ã³ng hoÃ n toÃ n.");

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
    /// KhÃ´i phá»¥c kho vá» tráº¡ng thÃ¡i cÅ© khi huá»· hoáº·c timeout.
    /// </summary>
    public void RestoreFromClosing(DepotStatus previousStatus)
    {
        if (Status != DepotStatus.Unavailable)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Chá»‰ cÃ³ thá»ƒ khÃ´i phá»¥c kho tá»« tráº¡ng thÃ¡i Unavailable.");

        if (previousStatus != DepotStatus.Available)
            throw new InvalidDepotStatusTransitionException(Status, previousStatus,
                "Tráº¡ng thÃ¡i khÃ´i phá»¥c khÃ´ng há»£p lá»‡. Chá»‰ cÃ³ thá»ƒ khÃ´i phá»¥c vá» Available.");

        Status = previousStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AddHistory(IEnumerable<DepotManagerAssignment> history)
    {
        _managerHistory.AddRange(history);
    }

    /// <summary>
    /// Cáº­p nháº­t má»©c sá»­ dá»¥ng kho dá»±a trÃªn thá»ƒ tÃ­ch vÃ  cÃ¢n náº·ng.
    /// </summary>
    /// <param name="volumeAmount">Tá»•ng thá»ƒ tÃ­ch cáº§n thÃªm (dmÂ³). Pháº£i > 0.</param>
    /// <param name="weightAmount">Tá»•ng cÃ¢n náº·ng cáº§n thÃªm (kg). Pháº£i > 0.</param>
    public void UpdateUtilization(decimal volumeAmount, decimal weightAmount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable)
            throw new DepotClosingException("Kho Ä‘ang ngÆ°ng hoáº¡t Ä‘á»™ng (Unavailable), khÃ´ng thá»ƒ thá»±c hiá»‡n thao tÃ¡c nÃ y.");

        if (volumeAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(volumeAmount, "thá»ƒ tÃ­ch");

        if (weightAmount <= 0)
            throw new InvalidDepotUtilizationAmountException(weightAmount, "cÃ¢n náº·ng");

        if (CurrentUtilization + volumeAmount > Capacity)
            throw new DepotCapacityExceededException("Thá»ƒ tÃ­ch kho khÃ´ng Ä‘á»§ chá»©a lÆ°á»£ng hÃ ng nháº­p vÃ o.");

        if (CurrentWeightUtilization + weightAmount > WeightCapacity)
            throw new DepotCapacityExceededException("CÃ¢n náº·ng kho khÃ´ng Ä‘á»§ chá»©a lÆ°á»£ng hÃ ng nháº­p vÃ o.");

        CurrentUtilization += volumeAmount;
        CurrentWeightUtilization += weightAmount;
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
    /// Gá»¡ manager Ä‘ang active (soft-unassign): set UnassignedAt, giá»¯ lá»‹ch sá»­.
    /// Chá»‰ cho phÃ©p khi kho á»Ÿ tráº¡ng thÃ¡i Available.
    /// Sau khi gá»¡, status chuyá»ƒn vá» PendingAssignment.
    /// </summary>
    public void UnassignManager()
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (Status == DepotStatus.Unavailable)
            throw new DepotClosingException(
                "Kho Ä‘ang ngÆ°ng hoáº¡t Ä‘á»™ng (Unavailable), khÃ´ng thá»ƒ gá»¡ quáº£n lÃ½.");

        var activeAssignment = _managerHistory.FirstOrDefault(x => x.IsActive());
        activeAssignment?.Unassign(DateTime.UtcNow);

        Status = DepotStatus.PendingAssignment;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // â”€â”€ Inventory lines (item-level stock, loaded from DepotSupplyInventory) â”€â”€
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
/// Äáº¡i diá»‡n cho sá»‘ lÆ°á»£ng tá»“n kho kháº£ dá»¥ng cá»§a má»™t loáº¡i váº­t tÆ° trong kho.
/// AvailableQuantity = Quantity - ReservedQuantity.
/// </summary>
public record DepotInventoryLine(
    int? ItemModelId,
    string ItemName,
    string? Unit,
    int AvailableQuantity
);






