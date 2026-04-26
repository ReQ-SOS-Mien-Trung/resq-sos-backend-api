using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

namespace RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

public class AddSosRequestToClusterResponse
{
    public int ClusterId { get; set; }
    public List<int> AddedSosRequestIds { get; set; } = [];
    public SosClusterDto? UpdatedCluster { get; set; }
}
