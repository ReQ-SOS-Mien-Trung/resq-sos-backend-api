using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;

public record GetAssemblyEventsQuery(
    int AssemblyPointId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResult<AssemblyEventListItemDto>>;
