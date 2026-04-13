namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Hình thức xử lý hàng tồn kho bên ngoài khi đóng kho.
/// </summary>
public enum ExternalDispositionType
{
    DonatedToOrganization,
    Liquidated,
    Disposed,
    Other
}
