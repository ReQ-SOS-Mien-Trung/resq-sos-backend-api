using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions
{
    public sealed class InvalidDepotManagerException : DomainException
    {
        public InvalidDepotManagerException()
            : base("Depot manager is invalid. A depot must be assigned to a valid manager.")
        {
        }
    }
}
