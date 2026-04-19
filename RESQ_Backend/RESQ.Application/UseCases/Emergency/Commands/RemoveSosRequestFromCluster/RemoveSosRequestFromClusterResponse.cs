using RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

namespace RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;

public class RemoveSosRequestFromClusterResponse
{
    public int ClusterId { get; set; }
    public int RemovedSosRequestId { get; set; }
    public bool IsClusterDeleted { get; set; }
    public SosClusterDto? UpdatedCluster { get; set; }
}
