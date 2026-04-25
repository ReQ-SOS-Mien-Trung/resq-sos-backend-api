using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class DepotManagerAlreadyAssignedException : DomainException
{
    public DepotManagerAlreadyAssignedException(Guid managerId)
        : base($"Quản lý (ID = {managerId}) đã được gán active cho kho này rồi.")
    {
    }
}
