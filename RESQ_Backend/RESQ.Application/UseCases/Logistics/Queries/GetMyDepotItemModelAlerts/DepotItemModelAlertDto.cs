namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;

public class DepotItemModelAlertDto
{
    public string AlertType { get; set; } = string.Empty;
    public string AlertTypeLabel { get; set; } = string.Empty;
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int ThresholdDays { get; set; }
    public DateTime ReferenceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DueInDays { get; set; }
    public int AffectedQuantity { get; set; }
    public int AffectedRecordCount { get; set; }
    public int ActionableQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ExpiringItemModelAlertRawDto
{
    public int LotId { get; set; }
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int RemainingQuantity { get; set; }
    public DateTime ExpiredDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
}

public class MaintenanceItemModelAlertRawDto
{
    public int ReusableItemId { get; set; }
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
}
