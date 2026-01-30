using RESQ.Application.UseCases.Resources.Queries.Depot;

namespace RESQ.Application.UseCases.Resources.Queries.GetAllDepots;

public class GetAllDepotsResponse
{
    public List<DepotDto> Depots { get; set; } = [];
}
