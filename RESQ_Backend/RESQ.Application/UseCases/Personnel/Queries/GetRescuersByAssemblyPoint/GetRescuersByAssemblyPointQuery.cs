using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;

public record GetRescuersByAssemblyPointQuery(
    int AssemblyPointId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResult<RescuerByAssemblyPointDto>>;
