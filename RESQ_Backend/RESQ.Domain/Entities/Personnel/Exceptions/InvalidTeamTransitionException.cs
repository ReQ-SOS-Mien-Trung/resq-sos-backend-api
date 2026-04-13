using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class InvalidTeamTransitionException : DomainException
{
    public InvalidTeamTransitionException(RescueTeamStatus current, RescueTeamStatus target) 
        : base($"Không thể chuyển trạng thái đội từ {current} sang {target}.") { }
}
