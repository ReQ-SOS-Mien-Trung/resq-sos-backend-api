using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public record GetRescuersQuery(
    int PageNumber,
    int PageSize,
    bool? IsBanned,
    string? Search,
    RescuerType? RescuerType
) : IRequest<PagedResult<GetRescuersItemResponse>>;
