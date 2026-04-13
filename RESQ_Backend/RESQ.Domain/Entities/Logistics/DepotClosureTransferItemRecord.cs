namespace RESQ.Domain.Entities.Logistics;

/// <summary>
/// Snapshot một dòng vật phẩm được gán vào một transfer trong luồng đóng kho.
/// </summary>
public class DepotClosureTransferItemRecord
{
    public int Id { get; private set; }
    public int TransferId { get; private set; }
    public int ItemModelId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string? Unit { get; private set; }
    public int Quantity { get; private set; }

    private DepotClosureTransferItemRecord() { }

    public static DepotClosureTransferItemRecord Create(
        int itemModelId,
        string itemName,
        string itemType,
        string? unit,
        int quantity)
    {
        return new DepotClosureTransferItemRecord
        {
            ItemModelId = itemModelId,
            ItemName = itemName,
            ItemType = itemType,
            Unit = unit,
            Quantity = quantity
        };
    }

    public static DepotClosureTransferItemRecord FromPersistence(
        int id,
        int transferId,
        int itemModelId,
        string itemName,
        string itemType,
        string? unit,
        int quantity)
    {
        return new DepotClosureTransferItemRecord
        {
            Id = id,
            TransferId = transferId,
            ItemModelId = itemModelId,
            ItemName = itemName,
            ItemType = itemType,
            Unit = unit,
            Quantity = quantity
        };
    }

    public void AttachToTransfer(int transferId)
    {
        TransferId = transferId;
    }
}
