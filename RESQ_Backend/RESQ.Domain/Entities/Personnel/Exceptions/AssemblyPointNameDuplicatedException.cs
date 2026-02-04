using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Personnel.Exceptions;

public sealed class AssemblyPointNameDuplicatedException : DomainException
{
    public AssemblyPointNameDuplicatedException(string name) 
        : base($"Điểm tập kết với tên '{name}' đã tồn tại.") { }
}