using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class InvalidAssemblyPointStatusTransitionException : DomainException
{
    public InvalidAssemblyPointStatusTransitionException(AssemblyPointStatus current, AssemblyPointStatus target, string reason) 
        : base($"Không thể chuyển trạng thái từ {current} sang {target}. Lý do: {reason}") 
    { 
    }
}
