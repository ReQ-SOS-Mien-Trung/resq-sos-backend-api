namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPointCheckInRadiusConfigs;

public class AssemblyPointCheckInRadiusConfigItem
{
    public int AssemblyPointId { get; set; }
    public double MaxRadiusMeters { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetAllAssemblyPointCheckInRadiusConfigsResponse
{
    public List<AssemblyPointCheckInRadiusConfigItem> Items { get; set; } = [];
    public int TotalCount => Items.Count;
}
