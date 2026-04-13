using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class AssemblyPointModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public AssemblyPointStatus Status { get; set; }
    public GeoLocation? Location { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>
    /// True khi Ä‘iá»ƒm táº­p káº¿t Ä‘ang cÃ³ sá»± kiá»‡n triá»‡u táº­p (Scheduled hoáº·c Gathering).
    /// GiÃ¡ trá»‹ nÃ y Ä‘Æ°á»£c tÃ­nh toÃ¡n khi query, khÃ´ng lÆ°u vÃ o DB.
    /// </summary>
    public bool HasActiveEvent { get; set; }

    public AssemblyPointModel() { }

    /// <summary>
    /// Táº¡o Ä‘iá»ƒm táº­p káº¿t má»›i â€” tráº¡ng thÃ¡i khá»Ÿi Ä‘áº§u lÃ  <see cref="AssemblyPointStatus.Created"/>.
    /// </summary>
    public static AssemblyPointModel Create(
        string code,
        string name,
        int maxCapacity,
        GeoLocation location,
        string? imageUrl = null)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        return new AssemblyPointModel
        {
            Code = code,
            Name = name,
            MaxCapacity = maxCapacity,
            Location = location,
            Status = AssemblyPointStatus.Created,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    /// <summary>
    /// Cáº­p nháº­t thÃ´ng tin Ä‘iá»ƒm táº­p káº¿t.
    /// KhÃ´ng Ä‘Æ°á»£c cáº­p nháº­t khi Ä‘ang <see cref="AssemblyPointStatus.Closed"/>.
    /// </summary>
    public void UpdateDetails(string name, int maxCapacity, GeoLocation location, string? imageUrl = null)
    {
        if (maxCapacity <= 0)
            throw new InvalidAssemblyPointCapacityException(maxCapacity);

        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        Name = name;
        MaxCapacity = maxCapacity;
        Location = location;
        if (imageUrl != null) ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Chuyá»ƒn tráº¡ng thÃ¡i theo state-flow Ä‘Æ°á»£c phÃ©p:
    /// <list type="bullet">
    ///   <item>Created â†’ Active</item>
    ///   <item>Active â†’ Overloaded | Unavailable | Closed</item>
    ///   <item>Overloaded â†’ Active | Unavailable (khÃ´ng thá»ƒ Closed trá»±c tiáº¿p)</item>
    ///   <item>Unavailable â†’ Active (Complete maintenance)</item>
    ///   <item>Closed â†’ (khÃ´ng cÃ³ chuyá»ƒn Ä‘á»•i nÃ o â€” viÄ©nh viá»…n)</item>
    /// </list>
    /// </summary>
    public void ChangeStatus(AssemblyPointStatus newStatus)
    {
        if (Status == newStatus) return;

        // Closed lÃ  tráº¡ng thÃ¡i cuá»‘i â€” khÃ´ng thá»ƒ thoÃ¡t ra
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        var allowed = Status switch
        {
            AssemblyPointStatus.Created          => new[] { AssemblyPointStatus.Active },
            AssemblyPointStatus.Active           => new[] { AssemblyPointStatus.Unavailable, AssemblyPointStatus.Closed },
                        // Theo state diagram: Unavailable chá»‰ cÃ³ thá»ƒ chuyá»ƒn vá» Active (Complete maintenance)
            AssemblyPointStatus.Unavailable => new[] { AssemblyPointStatus.Active },
            _                                    => Array.Empty<AssemblyPointStatus>()
        };

        if (!allowed.Contains(newStatus))
            throw new InvalidAssemblyPointStatusTransitionException(Status, newStatus,
                $"Tráº¡ng thÃ¡i cho phÃ©p tá»« {Status}: [{string.Join(", ", allowed)}].");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Kiá»ƒm tra sá»©c chá»©a trÆ°á»›c khi thÃªm <paramref name="additionalPersons"/> ngÆ°á»i vÃ o Ä‘iá»ƒm táº­p káº¿t.
    /// Throws náº¿u Ä‘iá»ƒm táº­p káº¿t khÃ´ng trong tráº¡ng thÃ¡i Active/Overloaded hoáº·c vÆ°á»£t sá»©c chá»©a.
    /// Tá»± Ä‘á»™ng chuyá»ƒn sang <see cref="AssemblyPointStatus.Overloaded"/> khi Ä‘áº¡t giá»›i háº¡n.
    /// </summary>
        /// <summary>
    /// Kiểm tra xem điểm tập kết có đang mở cửa để nhận thêm người không.
    /// Giờ đây không văng Exception nếu quá MaxCapacity (chỉ tính toán tỷ lệ ở DTO/UI).
    /// </summary>
    public void ValidatePersonCapacity(int currentPersonCount, int additionalPersons)
    {
        if (Status == AssemblyPointStatus.Closed)
            throw new AssemblyPointClosedException();

        if (Status != AssemblyPointStatus.Active)
            throw new AssemblyPointUnavailableException();
            
        // Removed hard block logic: no longer throw AssemblyPointCapacityExceededException
    }
}