using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public record GetRescuersQuery(
    int PageNumber,
    int PageSize,
    bool? IsBanned,
    string? Search
) : IRequest<PagedResult<GetRescuersItemResponse>>;
