using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

// Inherits DomainException -> Triggers HTTP 400
public sealed class InvalidDepotStatusTransitionException : DomainException
{
    public InvalidDepotStatusTransitionException(DepotStatus current, DepotStatus target, string reason) 
        : base($"Không thể chuyển trạng thái từ {current} sang {target}. Lý do: {reason}") 
    { 
    }
}
