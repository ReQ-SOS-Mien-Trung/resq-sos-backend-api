using RESQ.Application.UseCases.Logistics.Queries.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;

public class GetAllDepotsResponse
{
    public List<DepotDto> Depots { get; set; } = [];
}
