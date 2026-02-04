using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class InvalidGeoLocationException : DomainException
{
    public InvalidGeoLocationException(string message) : base(message) { }
}