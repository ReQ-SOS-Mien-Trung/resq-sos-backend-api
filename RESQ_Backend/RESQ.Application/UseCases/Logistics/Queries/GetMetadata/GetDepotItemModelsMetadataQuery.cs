using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetDepotItemModelsMetadataQuery : IRequest<List<DepotItemModelMetadataDto>>
{
    public Guid UserId { get; set; }
    public int DepotId { get; set; }
    public bool IsManager { get; set; }
}

public class DepotItemModelMetadataDto
{
    public int Key { get; set; }
    public string Value { get; set; } = string.Empty;
}
