using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public interface IMissionContextService
{
    /// <summary>
    /// Loads and prepares all data needed for AI mission planning:<br/>
    /// SOS requests in the cluster + nearby depots with inventory.
    /// Throws NotFoundException / BadRequestException on invalid input.
    /// </summary>
    Task<MissionContext> PrepareContextAsync(int clusterId, CancellationToken cancellationToken = default);
}

public class MissionContext
{
    public SosClusterModel Cluster { get; set; } = null!;
    public List<SosRequestSummary> SosRequests { get; set; } = [];
    public List<DepotSummary> NearbyDepots { get; set; } = [];
    public bool MultiDepotRecommended { get; set; }
}
