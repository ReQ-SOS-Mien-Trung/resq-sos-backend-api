using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Resources.Exceptions
{
    public class KeyNotFoundException : DomainException
    {
        public KeyNotFoundException(string name, int? id) : base($"{name} with id = {id} is not found.") { }
    }
}
