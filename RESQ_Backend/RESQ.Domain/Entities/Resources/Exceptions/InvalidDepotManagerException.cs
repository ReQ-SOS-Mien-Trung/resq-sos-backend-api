using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions
{
    public sealed class InvalidDepotManagerException : DomainException
    {
        public InvalidDepotManagerException()
            : base("Quản lý kho không hợp lệ. Kho phải được giao cho một quản lý hợp lệ.")
        {
        }
    }
}
