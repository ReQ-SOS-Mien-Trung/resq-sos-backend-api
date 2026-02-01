using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions
{
    public class KeyNotFoundException : DomainException
    {
        public KeyNotFoundException(string name, int? id) : base($"Không tìm thấy {name} với id = {id}.") { }
    }
}
