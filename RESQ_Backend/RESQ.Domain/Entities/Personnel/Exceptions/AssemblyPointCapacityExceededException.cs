namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class AssemblyPointCapacityExceededException : Exception
{
    public AssemblyPointCapacityExceededException(int capacity, int current, int adding)
        : base($"Điểm tập kết đã đạt giới hạn. Sức chứa: {capacity} đội, hiện tại: {current} đội, thêm: {adding} đội.")
    { }
}
