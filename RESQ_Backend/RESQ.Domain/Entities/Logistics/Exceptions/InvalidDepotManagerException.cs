using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions
{
    public sealed class InvalidDepotManagerException : DomainException
    {
        public InvalidDepotManagerException()
            : base("Qu?n lý kho không h?p l?. Kho ph?i du?c giao cho m?t qu?n lý h?p l?.")
        {
        }
    }
}
