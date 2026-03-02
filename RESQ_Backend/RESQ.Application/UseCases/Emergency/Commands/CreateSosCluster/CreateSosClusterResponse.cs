namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public class CreateSosClusterResponse
{
    public int ClusterId { get; set; }
    public int SosRequestCount { get; set; }
    public List<int> SosRequestIds { get; set; } = [];
    public string? SeverityLevel { get; set; }
    public DateTime? CreatedAt { get; set; }
}
