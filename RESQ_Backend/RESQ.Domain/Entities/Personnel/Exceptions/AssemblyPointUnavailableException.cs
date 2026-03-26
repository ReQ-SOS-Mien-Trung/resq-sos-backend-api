using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class AssemblyPointUnavailableException : DomainException
{
    public AssemblyPointUnavailableException()
        : base("Điểm tập kết đang bảo trì hoặc chưa kích hoạt và không thể thực hiện thao tác này.")
    {
    }
}
