namespace RESQ.Domain.Enum.Logistics
{
    public enum DepotStatus
    {
        PendingAssignment,
        Available,
        Full,
        Closing, // Trạng thái trung gian khi admin đang thực hiện đóng kho
        Closed
    }
}
