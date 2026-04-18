using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

public record GetServiceZoneQuery : IRequest<List<GetServiceZoneResponse>>;

public record GetServiceZoneByIdQuery(int Id) : IRequest<GetServiceZoneResponse>;

public record GetAllServiceZoneQuery : IRequest<List<GetServiceZoneResponse>>;
