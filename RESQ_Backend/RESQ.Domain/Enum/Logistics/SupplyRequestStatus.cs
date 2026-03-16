namespace RESQ.Domain.Enum.Logistics;

/// <summary>Trạng thái từ góc nhìn kho nguồn (source depot).</summary>
public enum SourceDepotStatus
{
    Pending   = 0,
    Accepted  = 1,
    Shipped   = 2,
    Completed = 3,
    Rejected  = 4
}

/// <summary>Trạng thái từ góc nhìn kho yêu cầu (requesting depot).</summary>
public enum RequestingDepotStatus
{
    WaitingForApproval = 0,
    Approved           = 1,
    InTransit          = 2,
    Received           = 3,
    Rejected           = 4
}
