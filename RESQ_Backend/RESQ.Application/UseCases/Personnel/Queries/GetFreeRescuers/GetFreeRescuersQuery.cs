using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;

public record GetFreeRescuersQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PagedResult<FreeRescuerDto>>;