namespace RESQ.Domain.Entities.Logistics.ValueObjects;

/// <summary>
/// Một dải cảnh báo tồn kho.
/// Matching rule: From &lt;= ratio &lt; To (null To = không có giới hạn trên).
/// </summary>
public sealed record WarningBand(string Name, decimal From, decimal? To);
