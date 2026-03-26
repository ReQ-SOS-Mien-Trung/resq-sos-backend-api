using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;

public record GetMyAssemblyEventsQuery(
    Guid RescuerId,
    int PageNumber,
    int PageSize) : IRequest<PagedResult<MyAssemblyEventDto>>;
