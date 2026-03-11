using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class RescueTeamBusinessRuleException : DomainException
{
    public RescueTeamBusinessRuleException(string message) : base(message) { }
}