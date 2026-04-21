namespace RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;

/// <summary>Request body từ client.</summary>
public class UpsertAssemblyPointCheckInRadiusRequest
{
    /// <summary>Bán kính check-in riêng tính bằng mét (phải > 0).</summary>
    public double MaxRadiusMeters { get; set; }
}
