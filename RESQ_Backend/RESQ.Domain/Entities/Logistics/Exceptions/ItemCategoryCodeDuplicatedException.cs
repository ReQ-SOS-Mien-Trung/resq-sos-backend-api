using RESQ.Domain.Entities.Exceptions;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.Exceptions;

public sealed class ItemCategoryCodeDuplicatedException : DomainException
{
    public ItemCategoryCodeDuplicatedException(ItemCategoryCode code) 
        : base($"Danh mục với mã '{code}' đã tồn tại.") { }
}
