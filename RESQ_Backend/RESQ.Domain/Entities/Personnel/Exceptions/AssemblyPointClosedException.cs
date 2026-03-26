using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class AssemblyPointClosedException : DomainException
{
    public AssemblyPointClosedException()
        : base("Điểm tập kết đã đóng vĩnh viễn và không thể thực hiện bất kỳ thao tác nào.")
    {
    }
}
