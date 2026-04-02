using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public record GetSupplyRequestsQuery : IRequest<GetSupplyRequestsResponse>
{
    public Guid                   UserId           { get; init; }
    public SourceDepotStatus?     SourceStatus     { get; init; }
    public RequestingDepotStatus? RequestingStatus { get; init; }
    public int                    PageNumber       { get; init; } = 1;
    public int                    PageSize         { get; init; } = 10;
}
