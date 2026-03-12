using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class TeamMemberDomainException : DomainException
{
    public TeamMemberDomainException(string message) : base(message) { }
}