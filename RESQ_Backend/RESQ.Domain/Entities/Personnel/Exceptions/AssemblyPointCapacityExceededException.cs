namespace RESQ.Domain.Entities.Personnel.Exceptions;

public class AssemblyPointCapacityExceededException : Exception
{
    public AssemblyPointCapacityExceededException(int capacity, int current, int adding)
        : base($"Điểm tập kết đã đạt giới hạn. Sức chứa tối đa: {capacity} người, hiện tại: {current} người, thêm: {adding} người.")
    { }
}
