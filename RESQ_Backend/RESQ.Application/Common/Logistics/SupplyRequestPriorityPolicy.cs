using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.Logistics;

public record SupplyRequestPriorityTiming(
    int UrgentMinutes,
    int HighMinutes,
    int MediumMinutes);

public static class SupplyRequestPriorityPolicy
{
    public static readonly SupplyRequestPriorityTiming DefaultTiming = new(
        UrgentMinutes: 10,
        HighMinutes: 20,
        MediumMinutes: 30);

    public static DateTime ResolveAutoRejectAt(
        DateTime createdAtUtc,
        SupplyRequestPriorityLevel priorityLevel,
        SupplyRequestPriorityTiming timing)
        => createdAtUtc.AddMinutes(GetResponseWindowMinutes(priorityLevel, timing));

    public static int GetResponseWindowMinutes(
        SupplyRequestPriorityLevel priorityLevel,
        SupplyRequestPriorityTiming timing)
        => priorityLevel switch
        {
            SupplyRequestPriorityLevel.Urgent => timing.UrgentMinutes,
            SupplyRequestPriorityLevel.High => timing.HighMinutes,
            SupplyRequestPriorityLevel.Medium => timing.MediumMinutes,
            _ => timing.MediumMinutes
        };

    public static DateTime? ResolveHighEscalationAt(
        DateTime autoRejectAtUtc,
        SupplyRequestPriorityLevel priorityLevel,
        SupplyRequestPriorityTiming timing)
        => priorityLevel == SupplyRequestPriorityLevel.Medium
            ? autoRejectAtUtc.AddMinutes(-timing.HighMinutes)
            : null;

    public static DateTime? ResolveUrgentEscalationAt(
        DateTime autoRejectAtUtc,
        SupplyRequestPriorityLevel priorityLevel,
        SupplyRequestPriorityTiming timing)
        => priorityLevel switch
        {
            SupplyRequestPriorityLevel.Urgent => autoRejectAtUtc,
            SupplyRequestPriorityLevel.High => autoRejectAtUtc.AddMinutes(-timing.UrgentMinutes),
            SupplyRequestPriorityLevel.Medium => autoRejectAtUtc.AddMinutes(-timing.UrgentMinutes),
            _ => null
        };

    public static bool IsValid(SupplyRequestPriorityTiming timing)
        => timing.UrgentMinutes > 0
        && timing.UrgentMinutes < timing.HighMinutes
        && timing.HighMinutes < timing.MediumMinutes;
}
