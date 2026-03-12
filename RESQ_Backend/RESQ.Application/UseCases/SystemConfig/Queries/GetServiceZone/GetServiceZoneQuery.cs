using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateServiceZone;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetServiceZone;

public record GetServiceZoneQuery : IRequest<GetServiceZoneResponse>;

public record GetServiceZoneByIdQuery(int Id) : IRequest<GetServiceZoneResponse>;
