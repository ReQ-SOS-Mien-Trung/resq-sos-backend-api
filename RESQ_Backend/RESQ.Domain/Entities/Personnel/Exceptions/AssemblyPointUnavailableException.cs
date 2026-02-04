using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class AssemblyPointUnavailableException : DomainException
{
    public AssemblyPointUnavailableException() 
        : base("Điểm tập kết đang tạm ngưng hoạt động và không thể thực hiện cập nhật.") 
    { 
    }
}