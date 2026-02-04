using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class AssemblyPointCodeDuplicatedException : DomainException
{
    public AssemblyPointCodeDuplicatedException(string code) 
        : base($"Điểm tập kết với mã '{code}' đã tồn tại.") { }
}