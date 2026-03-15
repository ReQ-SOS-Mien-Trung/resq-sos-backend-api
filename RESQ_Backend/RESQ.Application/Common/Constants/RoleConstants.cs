namespace RESQ.Application.Common.Constants;

/// <summary>
/// Role IDs tương ứng với bảng <c>roles</c> trong database.
/// </summary>
public static class RoleConstants
{
    public const int Admin       = 1;
    public const int Coordinator = 2;  // Coordinator (Tổng) – phạm vi: toàn hệ thống
    public const int Rescuer     = 3;  // Rescuer (Core + Volunteer) – phạm vi: Team
    public const int Manager     = 4;  // Depot Manager – phạm vi: toàn hệ thống / kho được giao
    public const int Victim      = 5;  // Victim / Public User – tạo SOS
}
